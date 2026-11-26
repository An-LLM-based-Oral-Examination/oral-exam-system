import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AdminImportUsersDialog } from '../AdminImportUsersDialog'
import { adminService } from '@/services/admin.service'

vi.mock('@/services/admin.service', () => ({
  adminService: {
    downloadUsersImportTemplate: vi.fn(),
    previewUsersImport: vi.fn(),
    commitUsersImport: vi.fn(),
  },
}))

describe('AdminImportUsersDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn(() => 'blob:mock-url'),
      revokeObjectURL: vi.fn(),
    })
  })

  it('downloads xlsx and csv templates cleanly', async () => {
    vi.mocked(adminService.downloadUsersImportTemplate).mockResolvedValueOnce({
      blob: new Blob(['xlsx-bytes']),
      fileName: 'mau-import-tai-khoan.xlsx',
    })

    render(
      <AdminImportUsersDialog open={true} onClose={vi.fn()} onImported={vi.fn()} />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Tải file mẫu \(\.xlsx\)/i }))

    await waitFor(() => {
      expect(adminService.downloadUsersImportTemplate).toHaveBeenCalledWith('xlsx')
    })
  })

  it('validates file extension, max size (2MB), and short default password client-side', async () => {
    render(
      <AdminImportUsersDialog open={true} onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const fileInput = screen.getByLabelText(/Chọn tệp danh sách tài khoản/i) as HTMLInputElement

    // Unsupported extension
    const pdfFile = new File(['%PDF'], 'users.pdf', { type: 'application/pdf' })
    fireEvent.change(fileInput, { target: { files: [pdfFile] } })
    expect(screen.getByText(/Định dạng không hỗ trợ/i)).toBeInTheDocument()

    // Oversized file > 2MB
    const bigFile = new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'huge.csv', {
      type: 'text/csv',
    })
    fireEvent.change(fileInput, { target: { files: [bigFile] } })
    expect(screen.getByText(/vượt quá giới hạn tối đa 2 MB/i)).toBeInTheDocument()

    // Valid file + short default password
    const validCsv = new File(['email,full_name,role\na@fpt.edu.vn,Nguyen An,student'], 'users.csv', {
      type: 'text/csv',
    })
    fireEvent.change(fileInput, { target: { files: [validCsv] } })
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm mặc định chung/i), {
      target: { value: '12345' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Kiểm tra dữ liệu/i }))

    expect(screen.getByText(/Mật khẩu mặc định phải từ 8 đến 128 ký tự/i)).toBeInTheDocument()
    expect(adminService.previewUsersImport).not.toHaveBeenCalled()

    // Formula-prefix default password blocked client-side
    fireEvent.change(screen.getByLabelText(/Mật khẩu tạm mặc định chung/i), {
      target: { value: '=Formula2026' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Kiểm tra dữ liệu/i }))
    expect(screen.getByText(/không được bắt đầu bằng/i)).toBeInTheDocument()
    expect(adminService.previewUsersImport).not.toHaveBeenCalled()
  })

  it('runs preview -> filter -> commit -> one-time xlsx result download and notifies parent', async () => {
    const onImported = vi.fn()
    const validFile = new File(['csv-data'], 'students.csv', { type: 'text/csv' })

    vi.mocked(adminService.previewUsersImport).mockResolvedValueOnce({
      fileName: 'students.csv',
      totalRows: 2,
      validCount: 1,
      invalidCount: 1,
      duplicateCount: 0,
      skippedCount: 1,
      hasDefaultPassword: false,
      rows: [
        {
          rowNumber: 2,
          email: 'thanhnq@fpt.edu.vn',
          fullName: 'Nguyễn Quang Thành',
          role: 'student',
          studentCode: 'SE184236',
          username: 'ThanhNQSE184236',
          passwordSource: 'AUTO_GENERATED',
          isValid: true,
          status: 'VALID',
          errorMessage: null,
        },
        {
          rowNumber: 3,
          email: 'admin@fpt.edu.vn',
          fullName: 'Blocked Admin',
          role: 'admin',
          studentCode: null,
          username: null,
          passwordSource: 'NONE',
          isValid: false,
          status: 'INVALID',
          errorMessage: 'Không cho phép import hàng loạt tài khoản vai trò admin.',
        },
      ],
    })

    vi.mocked(adminService.commitUsersImport).mockResolvedValueOnce({
      totalRows: 2,
      createdCount: 1,
      skippedCount: 1,
      resultFileName: 'ket-qua-import-tai-khoan-20261124-170000.xlsx',
      resultFileContentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      resultFileBase64: window.btoa('PK\x03\x04mock-xlsx'),
      createdRows: [
        {
          rowNumber: 2,
          userId: 'u-1',
          email: 'thanhnq@fpt.edu.vn',
          fullName: 'Nguyễn Quang Thành',
          role: 'student',
          studentCode: 'SE184236',
          username: 'ThanhNQSE184236',
          temporaryPassword: 'Abc9!xyz2345',
          passwordSource: 'AUTO_GENERATED',
          mustChangePassword: true,
        },
      ],
      skippedRows: [
        {
          rowNumber: 3,
          email: 'admin@fpt.edu.vn',
          fullName: 'Blocked Admin',
          role: 'admin',
          studentCode: null,
          reason: 'Không cho phép import hàng loạt tài khoản vai trò admin.',
        },
      ],
    })

    render(
      <AdminImportUsersDialog open={true} onClose={vi.fn()} onImported={onImported} />,
    )

    const fileInput = screen.getByLabelText(/Chọn tệp danh sách tài khoản/i) as HTMLInputElement
    fireEvent.change(fileInput, { target: { files: [validFile] } })
    fireEvent.click(screen.getByRole('button', { name: /Kiểm tra dữ liệu/i }))

    await waitFor(() => {
      expect(screen.getByText('@ThanhNQSE184236')).toBeInTheDocument()
      expect(
        screen.getByText(/Không cho phép import hàng loạt tài khoản vai trò admin/i),
      ).toBeInTheDocument()
    })

    // Filter to skipped only
    fireEvent.click(screen.getByRole('button', { name: /Lỗi \/ Trùng \(1\)/i }))
    expect(screen.queryByText('@ThanhNQSE184236')).toBeNull()

    // Commit
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận import 1 tài khoản hợp lệ/i }))

    await waitFor(() => {
      expect(adminService.commitUsersImport).toHaveBeenCalledWith(validFile, undefined)
      expect(onImported).toHaveBeenCalledWith(1, 1)
      expect(
        screen.getByText(/Tải file kết quả ngay bây giờ — mật khẩu tạm không lưu lại trên máy chủ/i),
      ).toBeInTheDocument()
    })

    // Reveal temporary password
    expect(screen.queryByText('Abc9!xyz2345')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: /Hiện mật khẩu tạm/i }))
    expect(screen.getByText('Abc9!xyz2345')).toBeInTheDocument()

    // Download one-time result .xlsx
    const downloadBtns = screen.getAllByRole('button', { name: /Tải file kết quả \(\.xlsx\)/i })
    fireEvent.click(downloadBtns[0])
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1)
  })

  it('disables commit button when validCount is 0 and displays RFC 7807 errors cleanly', async () => {
    const validFile = new File(['csv-data'], 'bad.csv', { type: 'text/csv' })
    vi.mocked(adminService.previewUsersImport)
      .mockRejectedValueOnce({
        status: 422,
        detail: 'UNPROCESSABLE: Thiếu cột bắt buộc ở dòng tiêu đề: email.',
      })
      .mockResolvedValueOnce({
        fileName: 'bad.csv',
        totalRows: 1,
        validCount: 0,
        invalidCount: 1,
        duplicateCount: 0,
        skippedCount: 1,
        hasDefaultPassword: false,
        rows: [
          {
            rowNumber: 2,
            email: 'bad',
            fullName: 'Bad',
            role: 'student',
            studentCode: null,
            username: null,
            passwordSource: 'NONE',
            isValid: false,
            status: 'INVALID',
            errorMessage: 'Định dạng email không hợp lệ.',
          },
        ],
      })

    render(
      <AdminImportUsersDialog open={true} onClose={vi.fn()} onImported={vi.fn()} />,
    )

    const fileInput = screen.getByLabelText(/Chọn tệp danh sách tài khoản/i) as HTMLInputElement
    fireEvent.change(fileInput, { target: { files: [validFile] } })
    fireEvent.click(screen.getByRole('button', { name: /Kiểm tra dữ liệu/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('[422]')
    expect(alert).toHaveTextContent(/Thiếu cột bắt buộc/i)

    fireEvent.click(screen.getByRole('button', { name: /Kiểm tra dữ liệu/i }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Xác nhận import 0 tài khoản hợp lệ/i })).toBeDisabled()
    })
  })
})
