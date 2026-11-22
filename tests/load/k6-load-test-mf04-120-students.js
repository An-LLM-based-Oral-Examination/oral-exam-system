import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';
import { sha256 } from 'k6/crypto';

// ==============================================================================
// FA26SE166 — MF-04 Official Oral Exam (BYOD Kiosk): 120-Student Load Test
// Topology: 5 Lab Rooms × 24 Students = 120 Concurrent VUs (per-vu-iterations)
// Flow: Password Login -> GET tickets/current (X-Device-Id) -> Check-In
//       (captures 1-time X-Kiosk-Session-Token) -> Audio Upload Auth & Optional
//       R2 PUT/Seal -> Persist-First Submit (PUT .../submission)
// Invariants: Persist-First Submit p95 < 100ms, 0 HTTP 5xx, 0 Lost Submissions.
// ==============================================================================

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5000').replace(/\/+$/, '');
const DEFAULT_PASSWORD = __ENV.STUDENT_PASSWORD;
const STUDENT_PREFIX = __ENV.STUDENT_PREFIX || 'student_';
const STUDENT_START_INDEX = Number(__ENV.STUDENT_START_INDEX || '1');
const SKIP_AUDIO_UPLOAD = String(__ENV.SKIP_AUDIO_UPLOAD || 'false').toLowerCase() === 'true';

if (!DEFAULT_PASSWORD) {
    throw new Error('Set STUDENT_PASSWORD environment variable before running MF-04 120-student load test.');
}

// Custom Metrics
const checkInDuration = new Trend('mf04_checkin_duration', true);
const submitDuration = new Trend('submit_duration', true);
const checkInSuccessRate = new Rate('mf04_checkin_success_rate');
const submitSuccessRate = new Rate('submit_success_rate');
const serverErrorCount = new Counter('server_error_count');
const lostSubmissionCount = new Counter('lost_submission_count');

export const options = {
    scenarios: {
        official_exam_5rooms_24seats_120_students: {
            executor: 'per-vu-iterations',
            vus: 120,
            iterations: 1,
            maxDuration: '5m',
        },
    },
    thresholds: {
        http_req_failed: ['rate<0.01'],
        'http_req_duration{type:checkin}': ['p(95)<500'],
        'http_req_duration{type:submit}': ['p(95)<100'],
        submit_duration: ['p(95)<100'],
        mf04_checkin_success_rate: ['rate>0.99'],
        submit_success_rate: ['rate>0.99'],
        server_error_count: ['count==0'],
        lost_submission_count: ['count==0'],
    },
};

function trackServerError(res) {
    if (res && res.status >= 500) {
        serverErrorCount.add(1);
    }
}

/**
 * Maps VU 1..120 -> 5 rooms (Room 1..5) × 24 seats (Seat 1..24) + unique BYOD X-Device-Id.
 */
function buildStudentContext(vuId) {
    const idx = STUDENT_START_INDEX + (vuId - 1);
    const padded = String(idx).padStart(3, '0');
    const roomIndex = Math.floor((vuId - 1) / 24) + 1;
    const seatNumber = ((vuId - 1) % 24) + 1;
    return {
        username: `${STUDENT_PREFIX}${padded}`,
        password: DEFAULT_PASSWORD,
        roomIndex,
        seatNumber,
        deviceId: `K6-BYOD-R${roomIndex}-S${String(seatNumber).padStart(2, '0')}-VU${padded}`,
    };
}

export default function () {
    const ctx = buildStudentContext(__VU);
    const jsonHeaders = { 'Content-Type': 'application/json' };

    // --------------------------------------------------------------------------
    // Step 1: Authenticate distinct student account
    // --------------------------------------------------------------------------
    const loginRes = http.post(
        `${BASE_URL}/api/v1/auth/password-login`,
        JSON.stringify({ username: ctx.username, password: ctx.password }),
        { headers: jsonHeaders, tags: { type: 'login' } }
    );
    trackServerError(loginRes);

    let accessToken = null;
    const loginOk = check(loginRes, {
        'login HTTP 200': (r) => r.status === 200,
        'login returned accessToken': (r) => {
            try {
                accessToken = JSON.parse(r.body).accessToken;
                return !!accessToken;
            } catch {
                return false;
            }
        },
    });
    if (!loginOk || !accessToken) {
        lostSubmissionCount.add(1);
        return;
    }

    const baseKioskHeaders = {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
        'X-Device-Id': ctx.deviceId,
        'X-Kiosk-Version': 'k6-loadtest-1.0.0',
    };

    // --------------------------------------------------------------------------
    // Step 2: Fetch assigned workstation ticket (BYOD)
    // --------------------------------------------------------------------------
    const ticketRes = http.get(
        `${BASE_URL}/api/v1/official-exams/tickets/current`,
        { headers: baseKioskHeaders, tags: { type: 'ticket' } }
    );
    trackServerError(ticketRes);

    let ticketId = null;
    const ticketOk = check(ticketRes, {
        'current ticket HTTP 200': (r) => r.status === 200,
        'ticket id present': (r) => {
            try {
                ticketId = JSON.parse(r.body).id;
                return !!ticketId;
            } catch {
                return false;
            }
        },
    });
    if (!ticketOk || !ticketId) {
        lostSubmissionCount.add(1);
        return;
    }

    // --------------------------------------------------------------------------
    // Step 3: BYOD Check-In -> lock X-Device-Id & capture 1-time X-Kiosk-Session-Token
    // --------------------------------------------------------------------------
    const checkInStart = Date.now();
    const checkInRes = http.post(
        `${BASE_URL}/api/v1/official-exams/tickets/${ticketId}/check-in`,
        JSON.stringify({ deviceId: ctx.deviceId, kioskVersion: 'k6-loadtest-1.0.0' }),
        { headers: baseKioskHeaders, tags: { type: 'checkin' } }
    );
    checkInDuration.add(Date.now() - checkInStart);
    trackServerError(checkInRes);

    let sessionToken = null;
    const checkInOk = check(checkInRes, {
        'check-in HTTP 200': (r) => r.status === 200,
        'check-in bound device & issued session token': (r) => {
            try {
                const body = JSON.parse(r.body);
                sessionToken = body.activeSessionToken;
                return body.status === 'IN_PROGRESS' && !!sessionToken;
            } catch {
                return false;
            }
        },
    });
    checkInSuccessRate.add(checkInOk);
    if (!checkInOk || !sessionToken) {
        lostSubmissionCount.add(1);
        return;
    }

    const sessionHeaders = {
        ...baseKioskHeaders,
        'X-Kiosk-Session-Token': sessionToken,
    };

    // Reload ticket with session token to obtain published exam questions
    const questionsRes = http.get(
        `${BASE_URL}/api/v1/official-exams/tickets/current`,
        { headers: sessionHeaders, tags: { type: 'questions' } }
    );
    trackServerError(questionsRes);

    let questions = [];
    try {
        questions = JSON.parse(questionsRes.body).questions || [];
    } catch {
        questions = [];
    }

    if (questions.length === 0) {
        lostSubmissionCount.add(1);
        return;
    }

    // --------------------------------------------------------------------------
    // Step 4: Authorize Audio Upload -> Upload WebM -> Seal Audio SHA-256
    // --------------------------------------------------------------------------
    if (!SKIP_AUDIO_UPLOAD) {
        const fakeWebmPayload = `WEBM-LOADTEST-MF04-${ctx.username}-R${ctx.roomIndex}-S${ctx.seatNumber}`;
        const audioHashHex = sha256(fakeWebmPayload, 'hex');
        const audioHashBase64 = sha256(fakeWebmPayload, 'base64');

        const authRes = http.post(
            `${BASE_URL}/api/v1/official-exams/tickets/${ticketId}/audio-upload-authorizations`,
            JSON.stringify({ expectedSha256Base64: audioHashBase64 }),
            { headers: sessionHeaders, tags: { type: 'audio_auth' } }
        );
        trackServerError(authRes);

        if (authRes.status !== 201) {
            lostSubmissionCount.add(1);
            return;
        }

        const authBody = JSON.parse(authRes.body);
        const putHeaders = Object.assign({}, authBody.requiredHeaders || { 'Content-Type': 'audio/webm' });
        const putRes = http.put(authBody.uploadUrl, fakeWebmPayload, {
            headers: putHeaders,
            tags: { type: 'r2_upload' },
        });
        trackServerError(putRes);

        const sealRes = http.put(
            `${BASE_URL}/api/v1/official-exams/tickets/${ticketId}/audio`,
            JSON.stringify({ objectKey: authBody.objectKey, sha256: audioHashHex }),
            { headers: sessionHeaders, tags: { type: 'audio_seal' } }
        );
        trackServerError(sealRes);

        if (sealRes.status !== 200) {
            lostSubmissionCount.add(1);
            return;
        }
    }

    sleep(0.2);

    // --------------------------------------------------------------------------
    // Step 5: Persist-First Submit (< 100ms p95 Invariant, 0 HTTP 5xx)
    // --------------------------------------------------------------------------
    const answers = questions.map((q) => {
        const rawText = `Sinh vien ${ctx.username} phong ${ctx.roomIndex} ghe ${ctx.seatNumber} tra loi cau hoi ${q.orderIndex} ve Clean Architecture va SOLID.`;
        return {
            examQuestionId: q.questionId,
            initialVoiceTranscript: rawText,
            transcriptWhisper: rawText,
            timestampsWhisper: null,
            timeSpentSeconds: 45,
            isManuallyEdited: false,
        };
    });

    const submitStart = Date.now();
    const submitRes = http.put(
        `${BASE_URL}/api/v1/official-exams/tickets/${ticketId}/submission`,
        JSON.stringify({ answers }),
        { headers: sessionHeaders, tags: { type: 'submit' } }
    );
    submitDuration.add(Date.now() - submitStart);
    trackServerError(submitRes);

    const submitOk = check(submitRes, {
        'submit HTTP 202 Accepted': (r) => r.status === 202,
        'submit persisted SUBMITTED and locked kiosk': (r) => {
            try {
                const body = JSON.parse(r.body);
                return (body.status === 'SUBMITTED' || body.status === 'AI_GRADED') && body.isKioskLocked === true;
            } catch {
                return false;
            }
        },
    });
    submitSuccessRate.add(submitOk);

    if (!submitOk) {
        lostSubmissionCount.add(1);
    }
}
