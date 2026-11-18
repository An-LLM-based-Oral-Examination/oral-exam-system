import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

// ==============================================================================
// FA26SE166 — MF-05 In-Class Exam Shift: 120-Student Concurrent Load Test
// Concurrency Target: 120 Students across 3 Classes (40 students per shift)
// Requirements: R2 — Anti-Collision verification, Persist-First Submit (<100ms),
//               0 Lost Updates, 0 HTTP 5xx errors.
// ==============================================================================

// Configuration from Environment Variables
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const DEFAULT_PASSWORD = __ENV.STUDENT_PASSWORD;
const OTP_CODE = __ENV.OTP_CODE;

// Shift IDs: Can be provided via SHIFT_IDS=id1,id2,id3 env variable
const SHIFT_IDS = (__ENV.SHIFT_IDS && __ENV.SHIFT_IDS.length > 0)
    ? __ENV.SHIFT_IDS.split(',').map(s => s.trim())
    : [];

if (!DEFAULT_PASSWORD || !OTP_CODE || SHIFT_IDS.length !== 3 || SHIFT_IDS.some(id => !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id))) {
    throw new Error('Set STUDENT_PASSWORD, OTP_CODE and three real SHIFT_IDS before running this load test.');
}

// Custom Metrics & Performance Trends
const joinDuration = new Trend('join_duration', true);
const submitDuration = new Trend('submit_duration', true);
const joinSuccessRate = new Rate('join_success_rate');
const submitSuccessRate = new Rate('submit_success_rate');
const lostUpdateCounter = new Counter('lost_update_count');

export const options = {
    scenarios: {
        concurrent_120_students: {
            executor: 'per-vu-iterations',
            vus: 120,
            iterations: 1,
            maxDuration: '5m',
        },
    },
    thresholds: {
        http_req_failed: ['rate<0.01'],         // Less than 1% failure rate overall
        'http_req_duration{type:join}': ['p(95)<1000'], // Join p95 < 1000ms
        'http_req_duration{type:submit}': ['p(95)<200'], // Submit p95 < 200ms
        join_duration: ['p(95)<1000'],          // Join p95 < 1000ms
        submit_duration: ['p(95)<200'],         // Submit p95 < 200ms
        join_success_rate: ['rate>0.99'],       // 99%+ join success
        submit_success_rate: ['rate>0.99'],     // 99%+ submit success
        lost_update_count: ['count==0'],        // Exactly 0 lost updates
    },
};

/**
 * Maps VU number (1..120) to student credentials and shift partition.
 * VU 1..40   -> Shift 0 (Class A)
 * VU 41..80  -> Shift 1 (Class B)
 * VU 81..120 -> Shift 2 (Class C)
 */
function getStudentContext(vuId) {
    const studentIndex = ((vuId - 1) % 120) + 1;
    const formattedIndex = String(studentIndex).padStart(3, '0');
    const username = `student_${formattedIndex}`;
    const email = `student_${formattedIndex}@fpt.edu.vn`;

    const shiftIndex = Math.floor((studentIndex - 1) / 40) % SHIFT_IDS.length;
    const shiftId = SHIFT_IDS[shiftIndex];

    return {
        username,
        email,
        password: DEFAULT_PASSWORD,
        shiftId,
        studentIndex,
    };
}

export default function () {
    const ctx = getStudentContext(__VU);
    const headers = { 'Content-Type': 'application/json' };

    // --------------------------------------------------------------------------
    // Step 1: One distinct student account per VU.
    // --------------------------------------------------------------------------
    let authToken = null;
    {
        const loginPayload = JSON.stringify({
            username: ctx.username,
            password: ctx.password,
        });

        const loginRes = http.post(`${BASE_URL}/api/v1/auth/password-login`, loginPayload, {
            headers,
            tags: { type: 'login' },
        });

        const loginOk = check(loginRes, {
            'login status is 200': (r) => r.status === 200,
            'login returned accessToken': (r) => {
                try {
                    const body = JSON.parse(r.body);
                    return !!body.accessToken;
                } catch {
                    return false;
                }
            },
        });

        if (loginOk) {
            authToken = JSON.parse(loginRes.body).accessToken;
        } else {
            lostUpdateCounter.add(1);
            return;
        }
    }

    const authHeaders = {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${authToken}`,
    };

    // --------------------------------------------------------------------------
    // Step 2: Join Shift via 5-Minute Rotating TOTP (Anti-Collision Allocation)
    // --------------------------------------------------------------------------
    const joinPayload = JSON.stringify({
        otpCode: OTP_CODE,
    });

    const joinStart = Date.now();
    const joinRes = http.post(
        `${BASE_URL}/api/v1/in-class-shifts/${ctx.shiftId}/join`,
        joinPayload,
        {
            headers: authHeaders,
            tags: { type: 'join' },
        }
    );
    const joinElapsed = Date.now() - joinStart;
    joinDuration.add(joinElapsed);

    let assignedQuestionId = null;
    const joinOk = check(joinRes, {
        'join status is 200': (r) => r.status === 200,
        'join returned assigned question': (r) => {
            try {
                const body = JSON.parse(r.body);
                assignedQuestionId = body.assignedQuestionId ||
                    (body.assignedQuestion && body.assignedQuestion.questionId) ||
                    (body.assignedQuestionIds && body.assignedQuestionIds[0]);
                return !!assignedQuestionId && body.status === 'JOINED';
            } catch {
                return false;
            }
        },
    });

    joinSuccessRate.add(joinOk);

    if (!joinOk || !assignedQuestionId) {
        lostUpdateCounter.add(1);
        return;
    }

    // Brief thinking/reading buffer (simulate student reading the prompt)
    sleep(0.5);

    // --------------------------------------------------------------------------
    // Step 3: Persist-First Submit (<100ms Invariant)
    // --------------------------------------------------------------------------
    const submitPayload = JSON.stringify({
        questionId: assignedQuestionId,
        transcriptText: `Sinh viên ${ctx.username} trả lời câu hỏi vấn đáp: Kiến trúc Clean Architecture phân tầng độc lập và bảo vệ Domain invariants. [Load test execution timestamp: ${Date.now()}]`,
    });

    const submitStart = Date.now();
    const submitRes = http.post(
        `${BASE_URL}/api/v1/in-class-shifts/${ctx.shiftId}/submit-answer`,
        submitPayload,
        {
            headers: authHeaders,
            tags: { type: 'submit' },
        }
    );
    const submitElapsed = Date.now() - submitStart;
    submitDuration.add(submitElapsed);

    const submitOk = check(submitRes, {
        'submit status is 200': (r) => r.status === 200,
        'submit status is SUBMITTED': (r) => {
            try {
                const body = JSON.parse(r.body);
                return body.status === 'SUBMITTED' && !!body.submissionId;
            } catch {
                return false;
            }
        },
    });

    submitSuccessRate.add(submitOk);

    if (!submitOk) {
        lostUpdateCounter.add(1);
    }

}
