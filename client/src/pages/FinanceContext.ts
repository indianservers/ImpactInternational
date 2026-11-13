import { createContext, useContext } from "react";
export const FinanceContext = createContext<Record<string, any>>({});
export const useFinance = () => useContext(FinanceContext);
