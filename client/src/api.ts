export type Workspace = {
  id: string;
  orgId: string;
  tenantId: string;
  organization: string;
  association: string;
  role: string;
  permissions: string[];
};
export type Org = {
  id: string;
  name: string;
  shortName?: string;
  code: string;
  levelId: string;
  parentId: string | null;
  level?: string;
  parent?: string;
  city: string;
  country: string;
  state?: string;
  address?: string;
  email?: string;
  phone?: string;
  website?: string;
  status: string;
  charterDate?: string;
  version: number;
  hasChildren?: boolean;
};
export type Level = {
  id: string;
  name: string;
  parentLevelId: string | null;
  isClub: boolean;
};
let csrf = "";
export async function api<T = any>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<T> {
  if (method !== "GET" && !csrf) {
    csrf = (await api<{ token: string }>("/auth/csrf")).token;
  }
  const r = await fetch("/api/v1" + path, {
    method,
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      ...(method === "GET" ? {} : { "X-CSRF": csrf }),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!r.ok) {
    let message = "Request failed";
    try {
      message = (await r.json()).detail || message;
    } catch {}
    throw new Error(message);
  }
  return r.status === 204 ? (undefined as T) : r.json();
}
