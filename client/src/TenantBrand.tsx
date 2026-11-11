import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "./api";
export function TenantBrand({ identity }: any) {
  const q = useQuery<any>({
    queryKey: ["public-tenant-branding"],
    queryFn: () => api("/branding"),
    staleTime: 60000,
  });
  const b = identity ?? q.data;
  useEffect(() => {
    document.title = b?.customDomain
      ? b.displayName + " · Community workspace"
      : "Impact International · Connected communities";
    const link = document.querySelector<HTMLLinkElement>('link[rel~="icon"]');
    if (link) {
      link.href = b?.favicon || "/favicon.svg";
      link.removeAttribute("type");
    }
  }, [b]);
  if (!b?.customDomain)
    return (
      <>
        Impact <span className="brand-light">International</span>
      </>
    );
  return (
    <>
      {b.logo && (
        <img className="intel-brand-logo" src={b.logo} alt="Association logo" />
      )}
      <span className="intel-brand-name" title={b.displayName}>
        {b.displayName}
      </span>
    </>
  );
}
