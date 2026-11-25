import { useEffect, useState } from 'react'
import { examPreparationService as service } from '@/services/examPreparation.service'
import { officialExamService } from '@/services/officialExam.service'
import { courseService } from '@/services/course.service'
import { courseCloService } from '@/services/courseClo.service'
import { lecturerAssignmentService } from '@/services/lecturerAssignment.service'
import { preparationError } from '@/lib/examPreparation'
import type {
  ExamBlueprint,
  ExamPaper,
  ExamPreparation,
  PaperStatus,
} from '@/types/examPreparation.types'
import type { CourseClo, ManagedCloCourse } from '@/types/courseClo.types'
import type { OfficialExamSession } from '@/types/officialExam.types'
interface DetailData {
  preparation?: ExamPreparation
  session?: OfficialExamSession
  course?: ManagedCloCourse
  papers: ExamPaper[]
  clos: CourseClo[]
  blueprint?: ExamBlueprint
  offeringId?: string
}
export function useExamPreparationDetail(
  sessionId: string,
  allowed: boolean,
  audience: 'head' | 'lecturer' = 'head',
) {
  const [revision, setRevision] = useState(0)
  const [resource, setResource] = useState<{
    sessionId: string
    revision: number
    data?: DetailData
    error?: string
  }>()
  useEffect(() => {
    if (!allowed || !sessionId) return
    const controller = new AbortController()
    async function load(): Promise<DetailData> {
      const [preparations, sessions, courses, offerings] = await Promise.all([
        service.list(sessionId, controller.signal),
        officialExamService.getSessions(),
        audience === 'head'
          ? courseCloService.getManagedCourses()
          : courseService.getLecturerCourses(),
        audience === 'head'
          ? lecturerAssignmentService.offerings(controller.signal)
          : Promise.resolve([]),
      ])
      const preparation = preparations.find((p) => p.sessionId === sessionId)
      const session = sessions.find((s) => s.id === sessionId)
      if (!preparation) return { preparation, session, papers: [], clos: [] }
      const [papers, clos, blueprint] = await Promise.all([
        service.papers(preparation.courseId, sessionId, controller.signal),
        courseCloService.list(preparation.courseId),
        preparation.examStructureId
          ? service.blueprint(preparation.examStructureId, controller.signal)
          : undefined,
      ])
      return {
        preparation,
        session,
        papers,
        clos,
        blueprint,
        course: courses.find((c) => c.id === preparation.courseId),
        offeringId: offerings.find(
          (o) =>
            o.courseId === preparation.courseId &&
            o.semesterId === preparation.semesterId &&
            o.isOfferingEnabled,
        )?.courseOfferingId,
      }
    }
    void load()
      .then((data) => {
        if (!controller.signal.aborted) setResource({ sessionId, revision, data })
      })
      .catch((error) => {
        if (!controller.signal.aborted)
          setResource((current) => ({
            sessionId,
            revision,
            data: current?.sessionId === sessionId ? current.data : undefined,
            error: preparationError(error),
          }))
      })
    return () => controller.abort()
  }, [allowed, sessionId, revision, audience])
  const refresh = () => setRevision((n) => n + 1)
  function updatePaper(id: string, status: PaperStatus, notes: string) {
    setResource((current) =>
      current?.sessionId === sessionId && current.data
        ? {
            ...current,
            data: {
              ...current.data,
              papers: current.data.papers.map((p) =>
                p.id === id ? { ...p, status, reviewNotes: notes || null } : p,
              ),
            },
          }
        : current,
    )
  }
  return {
    data: resource?.sessionId === sessionId ? resource.data : undefined,
    loading: resource?.sessionId !== sessionId || resource.revision !== revision,
    error: resource?.sessionId === sessionId ? resource.error : undefined,
    refresh,
    updatePaper,
  }
}
