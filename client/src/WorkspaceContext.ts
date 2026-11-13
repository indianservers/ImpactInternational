import { createContext, useContext } from "react";
import type { Workspace } from "./api";

// Keep the provider identity stable when the application shell is hot-reloaded.
// Lazy page modules and the shell must always consume the same context instance.
export const WorkspaceContext = createContext<{ w: Workspace | undefined; me: any }>({
  w: undefined,
  me: null,
});
export const useWorkspace = () => useContext(WorkspaceContext);
