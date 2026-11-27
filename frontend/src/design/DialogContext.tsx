import { useContext, type ReactNode } from 'react'
import { DialogContext } from './dialogContextState'

/** The selected page/tab or detail object, shared by its nested dialogs. */
export function DialogScope({ name, children }: { name: ReactNode; children: ReactNode }) {
  const parent = useContext(DialogContext)
  return <DialogContext.Provider value={name || parent}>{children}</DialogContext.Provider>
}

