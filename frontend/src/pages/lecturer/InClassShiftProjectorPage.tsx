import { Navigate, useParams } from 'react-router-dom'

/** Legacy fullscreen route — chiếu mã đã chuyển sang popup trên Hub/Results. */
export function InClassShiftProjectorPage() {
  const { shiftId = '' } = useParams()
  if (!shiftId) return <Navigate to="/in-class-shifts" replace />
  return <Navigate to={`/in-class-shifts/${shiftId}/results`} replace />
}
