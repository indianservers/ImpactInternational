import SectionTabs from "../SectionTabs";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, ArrowDown, ArrowUp, ShieldCheck } from "lucide-react";
import { api } from "../api";
import { PageHeader, Skeleton, ErrorBox, useWorkspace } from "../App";
export default function MembershipSettings() {
  const [section, setSection] = useState("Workflow");
  const { w } = useWorkspace(),
    qc = useQueryClient();
  const [stages, setStages] = useState<any[] | null>(null),
    [policy, setPolicy] = useState<any>(null),
    [error, setError] = useState<unknown>(),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["membership-settings", w?.id],
    queryFn: () => api("/membership/config"),
  });
  if (q.isPending) return <Skeleton />;
  const values =
    stages ??
    q.data?.workflow?.stages
      ?.sort((a: any, b: any) => a.sequence - b.sequence)
      .map((s: any) => ({
        name: s.name,
        requiredApprovals: s.requiredApprovals,
        roleIds: s.roles.map((r: any) => r.roleId),
      })) ??
    [];
  const p = policy ?? q.data?.policy;
  async function save(
    path: string,
    method: string,
    body: any,
    success: string,
  ) {
    setBusy(true);
    setError(null);
    try {
      await api(path, method, body);
      setMessage(success);
      qc.invalidateQueries({ queryKey: ["membership-settings"] });
      qc.invalidateQueries({ queryKey: ["member-config"] });
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  function update(i: number, change: any) {
    setStages(
      values.map((s: any, n: number) => (n === i ? { ...s, ...change } : s)),
    );
  }
  function move(i: number, d: number) {
    const next = [...values];
    [next[i], next[i + d]] = [next[i + d], next[i]];
    setStages(next);
  }
  return (
    <>
      <PageHeader
        eyebrow="CONFIGURE YOUR MEMBERSHIP JOURNEY"
        title="Membership settings"
        description="Impact membership categories, approval stages, and club transfer policies."
      />
      <ErrorBox error={error || q.error} />
      {message && (
        <div className="success" role="status">
          {message}
        </div>
      )}
      <SectionTabs id="membership-config" label="Membership configuration" tabs={["Workflow", "Categories", "Transfers"]} value={section} onChange={setSection} />
      <section className="panel" role="tabpanel" id="membership-config-panel-0" aria-labelledby="membership-config-tab-0" hidden={section !== "Workflow"}>
        <div className="section-heading">
          <div>
            <h2>Approval workflow</h2>
            <p>
              New versions apply to new submissions. Existing applications
              retain their original rules.
            </p>
          </div>
          <ShieldCheck size={23} />
        </div>
        <form
          className="form-stack"
          onSubmit={(e) => {
            e.preventDefault();
            save(
              "/membership/workflows",
              "POST",
              { stages: values },
              "A new workflow version was saved.",
            );
          }}
        >
          {values.map((s: any, i: number) => (
            <section className="workflow-stage-editor" key={i}>
              <div className="stage-number">{i + 1}</div>
              <div className="form-stack">
                <div className="form-grid">
                  <label>
                    Stage name
                    <input
                      required
                      value={s.name}
                      maxLength={100}
                      onChange={(e) => update(i, { name: e.target.value })}
                    />
                  </label>
                  <label>
                    Required distinct reviewers
                    <input
                      type="number"
                      required
                      min={1}
                      max={20}
                      value={s.requiredApprovals}
                      onChange={(e) =>
                        update(i, { requiredApprovals: Number(e.target.value) })
                      }
                    />
                  </label>
                </div>
                <label>
                  Authorized existing roles
                  <select
                    multiple
                    required
                    value={s.roleIds}
                    onChange={(e) =>
                      update(i, {
                        roleIds: [...e.target.selectedOptions].map(
                          (o) => o.value,
                        ),
                      })
                    }
                  >
                    {q.data?.roles
                      ?.filter((r: any) => r.canApprove)
                      .map((r: any) => (
                        <option key={r.id} value={r.id}>
                          {r.name}
                        </option>
                      ))}
                  </select>
                </label>
              </div>
              <div className="action-stack">
                <button
                  type="button"
                  className="icon-button"
                  disabled={i === 0}
                  aria-label={"Move stage " + (i + 1) + " up"}
                  onClick={() => move(i, -1)}
                >
                  <ArrowUp size={15} />
                </button>
                <button
                  type="button"
                  className="icon-button"
                  disabled={i === values.length - 1}
                  aria-label={"Move stage " + (i + 1) + " down"}
                  onClick={() => move(i, 1)}
                >
                  <ArrowDown size={15} />
                </button>
                <button
                  type="button"
                  className="text-button"
                  onClick={() =>
                    setStages(values.filter((_: any, n: number) => n !== i))
                  }
                >
                  Remove
                </button>
              </div>
            </section>
          ))}
          <div className="header-actions">
            <button
              className="secondary"
              type="button"
              onClick={() =>
                setStages([
                  ...values,
                  { name: "", requiredApprovals: 1, roleIds: [] },
                ])
              }
            >
              <Plus size={14} />
              Add approval stage
            </button>
            <button disabled={busy || !values.length}>
              Save new workflow version
            </button>
          </div>
        </form>
      </section>
      <div className="account-panels spaced">
        <section className="panel" role="tabpanel" id="membership-config-panel-1" aria-labelledby="membership-config-tab-1" hidden={section !== "Categories"}>
          <h2>Membership categories</h2>
          {q.data?.categories.map((c: any) => (
            <div className="context-row" key={c.id}>
              <span>
                {c.name}
                <small>{c.code}</small>
              </span>
              <b>
                {c.validityMonths
                  ? c.validityMonths + " months"
                  : "No fixed expiry"}
              </b>
            </div>
          ))}
          <form
            className="form-stack spaced"
            onSubmit={(e) => {
              e.preventDefault();
              const f = e.currentTarget,
                d = Object.fromEntries(new FormData(f));
              save(
                "/membership/categories",
                "POST",
                {
                  name: d.name,
                  code: d.code,
                  validityMonths: d.months ? Number(d.months) : null,
                  active: true,
                },
                "Membership category added.",
              ).then(() => f.reset());
            }}
          >
            <div className="form-grid">
              <label>
                Category name
                <input name="name" required maxLength={100} />
              </label>
              <label>
                Code
                <input name="code" required maxLength={20} />
              </label>
            </div>
            <label>
              Validity in months (blank means no fixed expiry)
              <input name="months" type="number" min={1} max={120} />
            </label>
            <button className="secondary" disabled={busy}>
              Add category
            </button>
          </form>
        </section>
        <section className="panel" role="tabpanel" id="membership-config-panel-2" aria-labelledby="membership-config-tab-2" hidden={section !== "Transfers"}>
          <h2>Affiliation & transfer policy</h2>
          {p && (
            <form
              className="form-stack spaced"
              onSubmit={(e) => {
                e.preventDefault();
                save(
                  "/membership/policy",
                  "PUT",
                  p,
                  "Membership policy updated.",
                );
              }}
            >
              {[
                [
                  "allowMultipleClubs",
                  "Allow affiliations with multiple clubs",
                ],
                ["sourceReviewRequired", "Require source-club transfer review"],
                [
                  "associationReviewRequired",
                  "Require Impact International transfer approval",
                ],
                [
                  "financialClearanceRequired",
                  "Require recorded administrative financial clearance",
                ],
              ].map(([k, l]) => (
                <label className="privacy-option" key={k}>
                  <span>{l}</span>
                  <input
                    type="checkbox"
                    checked={p[k]}
                    onChange={(e) => setPolicy({ ...p, [k]: e.target.checked })}
                  />
                </label>
              ))}
              <p>
                Clearance is a recorded administrative decision. No financial
                balance, settlement, or payment is inferred in this phase.
              </p>
              <button disabled={busy}>Save policy</button>
            </form>
          )}
        </section>
      </div>
    </>
  );
}
