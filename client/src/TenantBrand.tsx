import { impact } from "./brand";
export function TenantBrand({ identity: _identity }: any) {
  return <><img className="impact-brand-logo" src={impact.logo} alt="" width="48" height="48" /><span className="impact-brand-name">Impact <span className="brand-light">International</span><small>Training · Business · Service · Leadership</small></span></>;
}
