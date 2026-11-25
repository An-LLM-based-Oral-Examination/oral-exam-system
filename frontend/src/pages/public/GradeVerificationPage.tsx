import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import {
  AlertTriangle,
  ArrowLeft,
  CheckCircle2,
  LockKeyhole,
  Search,
  ShieldAlert,
  ShieldCheck,
} from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  GradeVerificationError,
  isValidCertificateHash,
  verifyGradeCertificate,
  type GradeCertificateVerification,
} from '@/services/gradeVerification.service'
import { ds } from '@/design/system'

function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('vi-VN')
}

export function GradeVerificationPage() {
  const { certificateHash: routeHash = '' } = useParams<{ certificateHash?: string }>()
  const navigate = useNavigate()
  const [hashInput, setHashInput] = useState(routeHash)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [result, setResult] = useState<GradeCertificateVerification | null>(null)

  const runVerification = useCallback(async (hash: string) => {
    const trimmed = hash.trim()
    if (!trimmed) {
      setResult(null)
      setError('')
      return
    }
    if (!isValidCertificateHash(trimmed)) {
      setResult(null)
      setError('Mã chứng thư số SHA-256 phải gồm đúng 64 ký tự thập lục phân (0–9, a–f).')
      return
    }

    setLoading(true)
    setError('')
    try {
      const verified = await verifyGradeCertificate(trimmed)
      setResult(verified)
    } catch (verifyError) {
      setResult(null)
      if (verifyError instanceof GradeVerificationError) {
        setError(verifyError.message)
      } else {
        setError('Không thể kết nối máy chủ để xác thực chứng thư bảng điểm.')
      }
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    setHashInput(routeHash)
    if (routeHash) {
      void runVerification(routeHash)
    } else {
      setResult(null)
      setError('')
    }
  }, [routeHash, runVerification])

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmed = hashInput.trim().toLowerCase()
    if (!isValidCertificateHash(trimmed)) {
      setResult(null)
      setError('Mã chứng thư số SHA-256 phải gồm đúng 64 ký tự thập lục phân (0–9, a–f).')
      return
    }
    if (trimmed !== routeHash.trim().toLowerCase()) {
      navigate(`/verify/grades/${encodeURIComponent(trimmed)}`)
    } else {
      void runVerification(trimmed)
    }
  }

  return (
    <div className="min-h-screen bg-[#f8fafc] px-4 py-8 text-[#0F2854]">
      <div className="mx-auto max-w-4xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[#d9e8f2] bg-white px-6 py-5 shadow-xs">
          <div className="flex items-center gap-3">
            <div className="grid size-11 place-items-center rounded-xl bg-[#0F2854] text-white">
              <ShieldCheck className="size-6" aria-hidden="true" />
            </div>
            <div>
              <p className="text-xs font-bold tracking-wider text-[#2563eb] uppercase">
                Xác thực chứng thư số PKI · MF-04
              </p>
              <h1 className="text-xl font-extrabold text-[#0F2854]">
                Tra cứu tính toàn vẹn biên bản bảng điểm
              </h1>
            </div>
          </div>
          <Link to="/login" className={ds.outlineButton}>
            <ArrowLeft className="size-4" /> Đăng nhập hệ thống
          </Link>
        </header>

        <section className="rounded-2xl border border-[#d9e8f2] bg-white p-6 shadow-xs">
          <form onSubmit={handleSubmit} className="space-y-3">
            <label htmlFor="certificate-hash-input" className="block text-sm font-bold text-[#0F2854]">
              Mã băm niêm phong SHA-256 (64 ký tự từ mã QR hoặc biên bản PDF)
            </label>
            <div className="flex flex-col gap-2 sm:flex-row">
              <input
                id="certificate-hash-input"
                type="text"
                value={hashInput}
                onChange={(event) => setHashInput(event.target.value)}
                placeholder="Nhập mã SHA-256 gồm 64 ký tự thập lục phân..."
                className="flex-1 rounded-xl border border-[#cbd5e1] bg-white px-4 py-2.5 font-mono text-sm text-[#0F2854] focus:border-[#2563eb] focus:ring-2 focus:ring-[#2563eb]/20 focus:outline-none"
                autoComplete="off"
                spellCheck={false}
              />
              <button type="submit" disabled={loading} className={ds.primaryButton}>
                <Search className="size-4" />
                {loading ? 'Đang xác thực…' : 'Xác thực chứng thư'}
              </button>
            </div>
          </form>
        </section>

        {error ? (
          <div
            role="alert"
            className="flex items-start gap-3 rounded-2xl border border-[#f3c2c8] bg-[#fdecec] px-5 py-4 text-sm font-semibold text-[#a43d4b]"
          >
            <ShieldAlert className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
            <div>
              <p className="font-bold">Xác thực không thành công</p>
              <p className="mt-1 font-medium">{error}</p>
            </div>
          </div>
        ) : null}

        {result ? (
          <section className="space-y-5">
            <div
              className={`flex flex-wrap items-center justify-between gap-4 rounded-2xl border px-6 py-5 ${
                result.isTampered
                  ? 'border-[#f3c2c8] bg-[#fdecec] text-[#a43d4b]'
                  : 'border-[#b7e4cf] bg-[#effaf6] text-[#176f58]'
              }`}
            >
              <div className="flex items-center gap-3">
                {result.isTampered ? (
                  <AlertTriangle className="size-7 shrink-0" aria-hidden="true" />
                ) : (
                  <CheckCircle2 className="size-7 shrink-0" aria-hidden="true" />
                )}
                <div>
                  <p className="text-xs font-bold tracking-wider uppercase">
                    {result.isTampered ? 'Cảnh báo dữ liệu bị thay đổi' : 'Chứng thư hợp lệ & toàn vẹn 100%'}
                  </p>
                  <h2 className="text-lg font-extrabold">
                    {result.courseCode} · {result.courseName} ({result.semesterCode})
                  </h2>
                </div>
              </div>
              <span className={result.isTampered ? ds.dangerChip : ds.successChip}>
                {result.status}
              </span>
            </div>

            <div className="grid gap-4 rounded-2xl border border-[#d9e8f2] bg-white p-6 shadow-xs sm:grid-cols-2">
              <div>
                <p className="text-xs font-bold text-[#5b6b84] uppercase">Đơn vị phát hành</p>
                <p className="mt-1 font-semibold text-[#0F2854]">{result.university || '—'}</p>
                <p className="text-xs text-[#5b6b84]">{result.digitalSignatureIssuer}</p>
              </div>
              <div>
                <p className="text-xs font-bold text-[#5b6b84] uppercase">Kỳ thi & Ca thi</p>
                <p className="mt-1 font-semibold text-[#0F2854]">
                  {result.sessionTitle} · {result.shiftName}
                </p>
                <p className="text-xs text-[#5b6b84]">
                  Phòng {result.roomLab} · Giám thị: {result.proctorName || '—'}
                </p>
              </div>
              <div>
                <p className="text-xs font-bold text-[#5b6b84] uppercase">Thời điểm ký & xác thực</p>
                <p className="mt-1 text-sm text-[#0F2854]">
                  Ký lúc: <strong>{formatDateTime(result.certificateSignedAt)}</strong>
                </p>
                <p className="text-xs text-[#5b6b84]">
                  Đối chiếu lúc: {formatDateTime(result.verifiedAtUtc)}
                </p>
              </div>
              <div>
                <p className="text-xs font-bold text-[#5b6b84] uppercase">Trạng thái khóa một chiều</p>
                <p className="mt-1 flex items-center gap-1.5 text-sm font-semibold text-[#0F2854]">
                  <LockKeyhole className="size-4 text-[#16a34a]" aria-hidden="true" />
                  {result.lockedTicketsCount} / {result.totalStudents} thí sinh đã khóa cứng
                </p>
                {result.certificateThumbprint ? (
                  <p className="mt-1 truncate font-mono text-xs text-[#5b6b84]">
                    Thumbprint: {result.certificateThumbprint}
                  </p>
                ) : null}
              </div>
              <div className="sm:col-span-2">
                <p className="text-xs font-bold text-[#5b6b84] uppercase">Mã băm SHA-256 đã xác minh</p>
                <code className="mt-1 block rounded-xl bg-[#f1f5f9] px-3 py-2 font-mono text-xs break-all text-[#0F2854]">
                  {result.certificateHash}
                </code>
              </div>
            </div>

            <div className="overflow-hidden rounded-2xl border border-[#d9e8f2] bg-white shadow-xs">
              <div className="border-b border-[#d9e8f2] px-6 py-4">
                <h3 className="text-sm font-bold text-[#0F2854]">
                  Bảng điểm đã niêm phong ({result.studentScores.length} thí sinh)
                </h3>
              </div>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[600px] text-left text-sm">
                  <thead className="bg-[#F4FBFD] text-xs font-bold text-[#5b6b84] uppercase">
                    <tr>
                      <th className="px-5 py-3">Ghế</th>
                      <th className="px-5 py-3">MSSV</th>
                      <th className="px-5 py-3">Họ và tên</th>
                      <th className="px-5 py-3">Điểm chính thức</th>
                      <th className="px-5 py-3">Khóa</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#d9e8f2]">
                    {result.studentScores.map((row) => (
                      <tr key={`${row.seatNumber}-${row.studentCode}`}>
                        <td className="px-5 py-3 font-mono text-xs font-bold text-[#5b6b84]">
                          #{row.seatNumber}
                        </td>
                        <td className="px-5 py-3 font-semibold text-[#0F2854]">{row.studentCode}</td>
                        <td className="px-5 py-3 text-[#334155]">{row.fullName}</td>
                        <td className="px-5 py-3 font-bold text-[#0F2854]">
                          {row.finalScore !== null ? row.finalScore.toFixed(2) : '—'}
                        </td>
                        <td className="px-5 py-3">
                          {row.isLocked ? (
                            <span className={ds.successChip}>LOCKED</span>
                          ) : (
                            <span className={ds.infoChip}>Mở</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        ) : null}
      </div>
    </div>
  )
}
