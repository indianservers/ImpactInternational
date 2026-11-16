import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import { Field, useAction } from "../GovernanceUI";
export function IciCareerProfile() {
  const { w } = useWorkspace(),
    qc = useQueryClient(),
    a = useAction(),
    [form, set] = useState<any>();
  const q = useQuery<any>({
    queryKey: ["ici", w?.id, "own-career"],
    queryFn: () => api("/ici/profile"),
  });
  useEffect(() => {
    if (q.data)
      set({ ...q.data, accountNumber: "", replaceBankDetails: false });
  }, [q.data]);
  if (q.isPending) return <Skeleton />;
  if (q.error) return <ErrorBox error={q.error} />;
  if (!form) return null;
  return (
    <section className="panel">
      <h2>My career & private profile</h2>
      <p>
        <Link to="/app/my-membership">
          Update name, contacts, gender, date of birth, address and certificates
        </Link>{" "}
        ·{" "}
        <Link to="/app/profile">
          Change password and manage account sessions
        </Link>
      </p>
      <form
        className="form-stack"
        onSubmit={async (e) => {
          e.preventDefault();
          try {
            await a.run("/ici/profile", "PUT", form);
            qc.invalidateQueries({ queryKey: ["ici", w?.id, "own-career"] });
          } catch {}
        }}
      >
        <Field
          label="Training career"
          name="trainingCareer"
          type="textarea"
          form={form}
          set={set}
        />
        <Field
          label="Leadership career"
          name="leadershipCareer"
          type="textarea"
          form={form}
          set={set}
        />
        {form.bankAllowed && (
          <fieldset>
            <legend>My private bank details</legend>
            <p>
              {form.hasBankDetails
                ? `${form.bankName} · ${form.accountHolder} · Account ending ${form.accountLast4}`
                : "No bank details recorded."}
            </p>
            <p>
              Only your own profile can access this information. Full account
              numbers are encrypted and are not returned by reports or member
              lists.
            </p>
            <label className="check-row">
              <input
                type="checkbox"
                checked={form.replaceBankDetails}
                onChange={(e) =>
                  set({ ...form, replaceBankDetails: e.target.checked })
                }
              />
              Replace my stored bank details
            </label>
            {form.replaceBankDetails && (
              <>
                {[
                  ["bankName", "Bank name"],
                  ["accountHolder", "Account holder"],
                  ["accountNumber", "Account number"],
                  ["routingCode", "IFSC / routing code"],
                ].map(([name, label]) => (
                  <Field
                    key={name}
                    label={label}
                    name={name}
                    type={name === "accountNumber" ? "password" : "text"}
                    form={form}
                    set={set}
                  />
                ))}
              </>
            )}
          </fieldset>
        )}
        <button disabled={a.busy}>Save my career profile</button>
        {a.feedback}
      </form>
    </section>
  );
}
