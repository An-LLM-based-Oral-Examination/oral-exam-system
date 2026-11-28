import { createContext, useContext, type ReactNode } from 'react'

export const DialogContext = createContext<ReactNode>(undefined)

export function useDialogContext() {
  return useContext(DialogContext)
}
