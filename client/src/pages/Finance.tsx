import ModuleNavigation from "../ModuleNavigation";
import { createContext, useContext, useState, useEffect } from "react";
import {
  Link,
  Route,
  Routes,
  useParams,
  useNavigate,
} from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowUpRight,
  Plus,
  ShieldCheck,
  Landmark,
  Receipt,
} from "lucide-react";
import { api } from "../api";
import { ErrorBox, Skeleton, useWorkspace } from "../App";
import "./Operations.css";
import "./Finance.css";
type Row = Record<string, any>;
const root = "/app/finance",
  ctx = createContext<Row>({});
const useFinance = () => useContext(ctx);
function useData(path: string, enabled = true) {
  const { w } = useWorkspace();
  return useQuery<any>({
    queryKey: ["finance", w?.id, path],
    queryFn: () => api(path),
    enabled,
    refetchInterval: 30000,
  });
}
function useAction() {
  const qc = useQueryClient();
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  async function run(path: string, body: unknown = {}, method = "POST") {
    setError(undefined);
    setBusy(true);
    try {
      const r = await api(path, method, body);
      await qc.invalidateQueries({ queryKey: ["finance"] });
      return r;
    } catch (e) {
      setError(e);
      throw e;
    } finally {
      setBusy(false);
    }
  }
  return { run, error, busy };
}
function money(v: any, currency = "USD") {
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency,
    maximumFractionDigits: 4,
  }).format(Number(v ?? 0));
}
function Badge({ children }: any) {
  return (
    <span
      className={
        "op-badge " +
        (/Paid|Verified|Approved|Settled|Completed/.test(String(children))
          ? "good"
          : "")
      }
    >
      {children}
    </span>
  );
}
function Header({ title, description, action }: any) {
  return (
    <header className="op-header">
      <div>
        <p className="op-eyebrow">IMPACT INTERNATIONAL / FINANCE</p>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action}
    </header>
  );
}
function Empty({ children }: any) {
  return (
    <div className="op-empty">
      <Landmark size={32} />
      <h3>A clear view of your finances</h3>
      <p>{children}</p>
    </div>
  );
}
function Table({
  rows,
  cols,
  onRow,
}: {
  rows: Row[];
  cols: [string, string, ((r: Row) => any)?][];
  onRow?: (r: Row) => void;
}) {
  return rows.length ? (
    <div className="fin-table-wrap">
      <table className="fin-table">
        <thead>
          <tr>
            {cols.map(([key, label]) => (
              <th key={key}>{label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, n) => (
            <tr key={r.id ?? n}>
              {cols.map(([key, , render], i) => (
                <td key={key}>
                  {i === 0 && onRow ? (
                    <button className="fin-link" onClick={() => onRow(r)}>
                      {render ? render(r) : String(r[key] ?? "—")}
                    </button>
                  ) : render ? (
                    render(r)
                  ) : (
                    String(r[key] ?? "—")
                  )}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ) : (
    <Empty>No records match this view. Create a record to begin.</Empty>
  );
}
type Field = {
  key: string;
  label: string;
  type?: string;
  options?: Row[] | string[];
  value?: any;
  required?: boolean;
  wide?: boolean;
};
function Form({
  title,
  fields,
  submit,
  close,
  description,
  details,
}: {
  title: string;
  fields: Field[];
  submit: (v: Row) => Promise<any>;
  close: () => void;
  description?: string;
  details?: any;
}) {
  const [error, setError] = useState<unknown>();
  const [busy, setBusy] = useState(false);
  return (
    <div className="op-scrim">
      <section
        className="op-dialog fin-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={title}
      >
        <div className="op-dialog-head">
          <h2>{title}</h2>
          <button aria-label="Close" onClick={close}>
            ×
          </button>
        </div>
        <p className="op-muted">{description}</p>
        {details}
        <form
          className="op-form"
          onSubmit={async (e) => {
            e.preventDefault();
            const data = new FormData(e.currentTarget);
            const value: Row = {};
            fields.forEach(
              (f) =>
                (value[f.key] =
                  f.type === "checkbox"
                    ? data.has(f.key)
                    : String(data.get(f.key) ?? "") || null),
            );
            setBusy(true);
            setError(undefined);
            try {
              await submit(value);
              close();
            } catch (e) {
              setError(e);
            } finally {
              setBusy(false);
            }
          }}
        >
          <div className="op-form-grid">
            {fields.map((f) => (
              <label
                className={"op-field " + (f.wide ? "fin-wide" : "")}
                key={f.key}
              >
                <span>{f.label}</span>
                {f.type === "invoiceLines" || f.type === "components" ? (
                  <LineEditor
                    name={f.key}
                    invoice={f.type === "invoiceLines"}
                  />
                ) : f.options ? (
                  <select
                    name={f.key}
                    defaultValue={f.value ?? ""}
                    required={f.required !== false}
                  >
                    <option value="">Select…</option>
                    {f.options.map((o: any) => (
                      <option
                        key={typeof o === "string" ? o : o.id}
                        value={typeof o === "string" ? o : o.id}
                      >
                        {typeof o === "string"
                          ? o
                          : (o.name ?? o.title ?? o.number)}
                      </option>
                    ))}
                  </select>
                ) : f.type === "textarea" ? (
                  <textarea
                    name={f.key}
                    defaultValue={f.value}
                    required={f.required !== false}
                  />
                ) : (
                  <input
                    name={f.key}
                    type={f.type ?? "text"}
                    step={f.type === "number" ? "any" : undefined}
                    defaultValue={f.type === "checkbox" ? undefined : f.value}
                    defaultChecked={
                      f.type === "checkbox" ? !!f.value : undefined
                    }
                    required={f.type !== "checkbox" && f.required !== false}
                  />
                )}
              </label>
            ))}
          </div>
          <ErrorBox error={error} />
          <button disabled={busy} className="op-primary">
            {busy ? "Saving…" : "Save record"}
          </button>
        </form>
      </section>
    </div>
  );
}
const today = () => new Date().toISOString().slice(0, 10);
const key = () => crypto.randomUUID();
export default function Finance() {
  const { w } = useWorkspace();
  const q = useData("/finance/entities");
  const [entityId, setEntity] = useState("");
  const [create, setCreate] = useState(false);
  const a = useAction();
  useEffect(() => {
    setEntity(sessionStorage.getItem("finance:" + w?.id) ?? "");
  }, [w?.id]);
  const e = q.data?.find((x: Row) => x.id === entityId) ?? q.data?.[0];
  const path = e ? "/finance/entities/" + e.id : "";
  const navigate = useNavigate();
  return (
    <ctx.Provider value={{ e, path, currency: e?.currency ?? "USD", a }}>
      <div className="operations finance">
        <div className="fin-context">
          <span>
            <Landmark size={18} /> Financial workspace
          </span>
          <select
            aria-label="Financial entity"
            value={e?.id ?? ""}
            onChange={(x) => {
              setEntity(x.target.value);
              sessionStorage.setItem("finance:" + w?.id, x.target.value);
            }}
          >
            <option value="" disabled>
              Select entity
            </option>
            {q.data?.map((x: Row) => (
              <option key={x.id} value={x.id}>
                {x.name} · {x.currency}
              </option>
            ))}
          </select>
          {w?.permissions.includes("finance.configure") && (
            <button onClick={() => setCreate(true)}>
              <Plus size={15} /> New entity
            </button>
          )}
        </div>
        <ModuleNavigation root={root} label="Finance" items={[["", "Overview"], ["invoices", "Invoices"], ["dues", "Membership dues"], ["payments", "Payments"], ["reconciliation", "Reconciliation"], ["accounts", "Accounting"], ["budgets", "Budgets"], ["expenses", "Expenses"], ["funding", "Donations & sponsors"], ["sources", "Events & projects"], ["allocations", "Inter-entity"], ["reports", "Reports"], ["settings", "Settings"], ["member", "My finances"]]} groups={{"Overview": ["", "reports", "member"], "Billing": ["invoices", "dues", "payments", "expenses", "funding", "sources", "allocations"], "Books & settings": ["accounts", "reconciliation", "budgets", "settings"]}} />
        <ErrorBox error={q.error ?? a.error} />
        <Routes>
          <Route path="member" element={<MemberFinance />} />
          <Route path="invoices/:id" element={<Invoice />} />
          <Route
            path="*"
            element={
              !q.data ? (
                <Skeleton />
              ) : !e ? (
                <>
                  <Header
                    title="Finance, with clarity"
                    description="Create a financial entity to establish its own currency, accounts and collection authority."
                  />
                  <Empty>
                    Your member invoices remain available in My finances. Access
                    to independent entity books requires an explicit grant.
                  </Empty>
                </>
              ) : (
                <Routes>
                  <Route index element={<Dashboard />} />
                  <Route path="invoices" element={<Invoices />} />
                  <Route path="dues" element={<Dues />} />
                  <Route path="payments" element={<Payments />} />
                  <Route path="reconciliation" element={<Reconciliation />} />
                  <Route path="accounts" element={<Accounting />} />
                  <Route path="budgets" element={<Budgets />} />
                  <Route path="expenses" element={<Expenses />} />
                  <Route path="funding" element={<Funding />} />
                  <Route path="sources" element={<Sources />} />
                  <Route path="allocations" element={<Allocations />} />
                  <Route path="reports" element={<Reports />} />
                  <Route path="settings" element={<Settings />} />
                </Routes>
              )
            }
          />
        </Routes>
        {create && (
          <Form
            title="Establish a financial entity"
            close={() => setCreate(false)}
            description="This creates separate books. Record legal collection authority before adding other organizations. Accounting currently uses accrual and one native currency."
            fields={[
              { key: "orgId", label: "Owning organization", value: w?.orgId },
              { key: "name", label: "Legal / financial entity name" },
              { key: "country", label: "Country" },
              { key: "currency", label: "Currency code", value: "USD" },
              {
                key: "decimals",
                label: "Currency decimal places",
                type: "number",
                value: 2,
              },
              {
                key: "fiscalMonth",
                label: "Fiscal start month",
                type: "number",
                value: 1,
              },
              { key: "address", label: "Invoice address", required: false },
              {
                key: "taxRegistration",
                label: "Tax registration",
                required: false,
              },
              {
                key: "collectionAuthority",
                label: "Collection authority",
                type: "textarea",
                value: "Own organization only",
              },
            ]}
            submit={async (v) => {
              await a.run("/finance/entities", {
                ...v,
                orgId: v.orgId,
                decimals: Number(v.decimals),
                fiscalMonth: Number(v.fiscalMonth),
                address: v.address ?? "",
                taxRegistration: v.taxRegistration ?? "",
              });
              navigate(root);
            }}
          />
        )}
      </div>
    </ctx.Provider>
  );
}
function Dashboard() {
  const { path, currency, e } = useFinance();
  const q = useData(path + "/dashboard");
  return (
    <>
      <Header
        title="Financial overview"
        description={
          e.name + " · Independent entity books, connected to your club."
        }
        action={
          <Link className="op-primary" to={root + "/invoices"}>
            Manage invoices <ArrowUpRight size={16} />
          </Link>
        }
      />
      {!q.data ? (
        <Skeleton />
      ) : (
        <>
          <div className="fin-hero">
            <div>
              <p>OUTSTANDING RECEIVABLES</p>
              <strong>{money(q.data.outstanding, currency)}</strong>
              <span>
                Across issued invoices · credits and verified allocations
                included
              </span>
            </div>
            <Receipt size={58} />
          </div>
          <div className="fin-stats">
            {[
              ["Verified this month", q.data.monthCollections],
              ["Overdue", q.data.overdue],
              ["Cash / bank book", q.data.cashBank],
              ["Awaiting settlement", q.data.unsettled],
            ].map(([label, v]) => (
              <section className="op-card" key={String(label)}>
                <p>{label}</p>
                <h2>{money(v, currency)}</h2>
              </section>
            ))}
          </div>
          <div className="fin-two">
            <section className="op-card">
              <h2>Recent invoices</h2>
              <InvoiceTable rows={q.data.recentInvoices} />
            </section>
            <section className="op-card">
              <h2>Recent verified collections</h2>
              <Table
                rows={q.data.recentPayments}
                cols={[
                  ["receiptNumber", "Receipt"],
                  ["payer", "Payer"],
                  ["amount", "Amount", (r) => money(r.amount, currency)],
                  [
                    "settlementStatus",
                    "Settlement",
                    (r) => <Badge>{r.settlementStatus}</Badge>,
                  ],
                ]}
              />
            </section>
          </div>
          <p className="fin-policy">
            <ShieldCheck size={17} />
            {q.data.policy}
          </p>
        </>
      )}
      <ErrorBox error={q.error} />
    </>
  );
}
function InvoiceTable({ rows }: any) {
  const navigate = useNavigate();
  return (
    <Table
      rows={rows}
      onRow={(r) => navigate(root + "/invoices/" + r.id)}
      cols={[
        ["number", "Invoice", (r) => r.number || "Draft invoice"],
        ["customer", "Billed to"],
        ["date", "Date"],
        ["total", "Total", (r) => money(r.total, r.currency)],
        ["outstanding", "Outstanding", (r) => money(r.outstanding, r.currency)],
        [
          "state",
          "State",
          (r) => (
            <Badge>
              {r.state} · {r.paymentStatus}
            </Badge>
          ),
        ],
      ]}
    />
  );
}
function PickerFields(options: Row) {
  return [
    {
      key: "personId",
      label: "Member / payer",
      options: options.people,
      required: false,
    },
    { key: "customer", label: "Billed name" },
    {
      key: "purpose",
      label: "Financial purpose",
      options: [
        "ClubDues",
        "AssociationDues",
        "DistrictDues",
        "NationalDues",
        "InternationalDues",
        "JoiningFee",
        "Renewal",
        "Event",
        "Project",
        "Donation",
        "Sponsorship",
        "Other",
      ],
    },
    { key: "date", label: "Invoice date", type: "date", value: today() },
    { key: "dueOn", label: "Due date", type: "date", value: today() },
    { key: "lines", label: "Invoice lines", type: "invoiceLines", wide: true },
    {
      key: "projectId",
      label: "Project",
      options: options.projects,
      required: false,
    },
    {
      key: "eventId",
      label: "Event",
      options: options.events,
      required: false,
    },
    { key: "notes", label: "Notes", type: "textarea", required: false },
  ] as Field[];
}
function Invoices() {
  const { path, a } = useFinance();
  const [search, setSearch] = useState("");
  const [after, setAfter] = useState("");
  const [open, setOpen] = useState(false);
  const q = useData(
    path +
      "/invoices?search=" +
      encodeURIComponent(search) +
      (after ? "&after=" + after : ""),
  );
  const opts = useData(path + "/options?search=" + encodeURIComponent(search));
  return (
    <>
      <Header
        title="Invoices"
        description="Frozen financial documents, with balances backed by verified allocations."
        action={
          <button className="op-primary" onClick={() => setOpen(true)}>
            <Plus size={16} /> Create invoice
          </button>
        }
      />
      <input
        className="fin-search"
        aria-label="Search invoices"
        placeholder="Search number or member…"
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setAfter("");
        }}
      />
      <ErrorBox error={q.error} />
      {q.data ? <InvoiceTable rows={q.data} /> : <Skeleton />}
      <PageButtons rows={q.data} setAfter={setAfter} />
      {open && opts.data && (
        <Form
          title="Create invoice"
          close={() => setOpen(false)}
          fields={PickerFields(opts.data)}
          submit={(v) =>
            a.run(path + "/invoices", {
              ...v,
              key: key(),
              notes: v.notes ?? "",
              lines: JSON.parse(v.lines),
            })
          }
        />
      )}
    </>
  );
}
function PageButtons({ rows, setAfter }: any) {
  return (
    <div className="fin-actions">
      <button onClick={() => setAfter("")}>First page</button>
      {rows?.length === 100 && (
        <button onClick={() => setAfter(rows[rows.length - 1].id)}>
          Next 100 →
        </button>
      )}
    </div>
  );
}
function Invoice() {
  const { w } = useWorkspace();
  const manages = w?.permissions.includes("finance.manage"),
    posts = w?.permissions.includes("finance.post");
  const { id } = useParams();
  const q = useData("/finance/invoices/" + id);
  const { a } = useFinance();
  const [form, setForm] = useState("");
  const [intentKey] = useState(() => key());
  const i = q.data?.invoice;
  return (
    <>
      <Header
        title={i?.number || "Invoice"}
        description="Invoice document state and payment balance are tracked separately."
      />
      {!i ? (
        <>
          <Skeleton />
          <ErrorBox error={q.error} />
        </>
      ) : (
        <>
          <div className="op-card fin-invoice">
            <div className="fin-actions">
              <Badge>{i.state}</Badge>
              <Badge>{i.paymentStatus}</Badge>
              <a
                href={"/api/v1/finance/invoices/" + id + "/pdf"}
                target="_blank"
                rel="noreferrer"
              >
                Download PDF
              </a>
            </div>
            <h2>{q.data.entityName}</h2>
            <p>
              Billed to <strong>{i.customer}</strong>
            </p>
            <p>
              {i.date} · Due {i.dueOn}
            </p>
            <Table
              rows={q.data.lines}
              cols={[
                ["description", "Description"],
                ["quantity", "Qty"],
                ["rate", "Rate", (r) => money(r.rate, i.currency)],
                ["discount", "Discount", (r) => money(r.discount, i.currency)],
                ["tax", "Tax", (r) => money(r.tax, i.currency)],
                ["total", "Total", (r) => money(r.total, i.currency)],
              ]}
            />
            <div className="fin-totals">
              <p>
                Total <strong>{money(i.total, i.currency)}</strong>
              </p>
              <p>
                Paid <strong>{money(i.paid, i.currency)}</strong>
              </p>
              <p>
                Credits <strong>{money(i.credited, i.currency)}</strong>
              </p>
              <p>
                Outstanding <strong>{money(i.outstanding, i.currency)}</strong>
              </p>
            </div>
            <p>{i.notes}</p>
            <div className="fin-actions">
              {i.state === "Draft" && manages && (
                <>
                  <button
                    className="op-primary"
                    onClick={() =>
                      a
                        .run("/finance/invoices/" + id + "/issue")
                        .catch(() => {})
                    }
                  >
                    Issue invoice
                  </button>
                  <button onClick={() => setForm("cancel")}>
                    Cancel draft
                  </button>
                </>
              )}
              {i.state === "Issued" && (
                <>
                  <button
                    onClick={() =>
                      a.run("/finance/invoices/" + id + "/send").catch(() => {})
                    }
                  >
                    Send notification
                  </button>
                  <button disabled={!posts} onClick={() => setForm("credit")}>
                    Credit note
                  </button>
                  {q.data.paymentAvailable && Number(i.outstanding) > 0 ? (
                    <button
                      className="op-primary"
                      onClick={async () => {
                        try {
                          const r = await a.run(
                            "/finance/invoices/" + id + "/payment-intents",
                            { key: intentKey },
                          );
                          window.location.assign(r.checkoutUrl);
                        } catch {}
                      }}
                    >
                      Pay now · Secure checkout
                    </button>
                  ) : (
                    !q.data.paymentAvailable && (
                      <span className="op-muted">
                        Online payment is not configured. Contact your Treasurer
                        for approved collection methods.
                      </span>
                    )
                  )}
                </>
              )}
            </div>
          </div>
          <section className="op-card">
            <h2>Credit note history</h2>
            <Table
              rows={q.data.credits}
              cols={[
                ["date", "Date"],
                ["amount", "Amount", (r) => money(r.amount, i.currency)],
                ["reason", "Reason"],
              ]}
            />
          </section>
          {form && (
            <Form
              title={form === "credit" ? "Issue credit note" : "Cancel draft"}
              close={() => setForm("")}
              fields={
                form === "credit"
                  ? [
                      { key: "amount", label: "Credit amount", type: "number" },
                      { key: "reason", label: "Reason", type: "textarea" },
                    ]
                  : [
                      {
                        key: "reason",
                        label: "Cancellation reason",
                        type: "textarea",
                      },
                    ]
              }
              submit={(v) =>
                a.run(
                  "/finance/invoices/" +
                    id +
                    (form === "credit" ? "/credits" : "/cancel"),
                  { ...v, key: key() },
                )
              }
            />
          )}
        </>
      )}
    </>
  );
}
function MemberFinance() {
  const q = useData("/my/finance");
  const registrations = useData("/my/registrations");
  const { a } = useFinance();
  const navigate = useNavigate();
  return (
    <>
      <Header
        title="My finances"
        description="Your dues, verified payments and receipts. Each currency is reported separately."
      />
      {!q.data ? (
        <Skeleton />
      ) : (
        <>
          <div className="fin-stats">
            {q.data.balances.map((b: Row) => (
              <section className="op-card" key={b.currency}>
                <p>Outstanding · {b.currency}</p>
                <h2>{money(b.outstanding, b.currency)}</h2>
                <p>Next due {b.due ?? "—"}</p>
              </section>
            ))}
          </div>
          <section className="op-card">
            <h2>Your invoices</h2>
            {registrations.data
              ?.filter((r: Row) => r.status === "Pending Payment")
              .map((r: Row) => (
                <p key={r.id}>
                  {r.title}{" "}
                  <button
                    onClick={async () => {
                      try {
                        const i = await a.run(
                          "/finance/registrations/" + r.id + "/invoice",
                        );
                        navigate(root + "/invoices/" + i.id);
                      } catch {}
                    }}
                  >
                    Get registration invoice
                  </button>
                </p>
              ))}
            <InvoiceTable rows={q.data.invoices} />
          </section>
          <section className="op-card">
            <h2>Payment history</h2>
            <Table
              rows={q.data.payments}
              cols={[
                ["date", "Date"],
                ["reference", "Reference"],
                ["amount", "Amount", (r) => money(r.amount, r.currency)],
                ["status", "Status", (r) => <Badge>{r.status}</Badge>],
                [
                  "receiptNumber",
                  "Receipt",
                  (r) =>
                    r.status === "Verified" ? (
                      <a href={"/api/v1/finance/payments/" + r.id + "/receipt"}>
                        Download receipt
                      </a>
                    ) : (
                      "Pending verification"
                    ),
                ],
              ]}
            />
          </section>
        </>
      )}
      <ErrorBox error={q.error} />
    </>
  );
}
function Dues() {
  const { path, a } = useFinance();
  const q = useData(path + "/dues-plans"),
    opts = useData(path + "/options"),
    jobs = useData(path + "/jobs");
  const [form, setForm] = useState("");
  const [plan, setPlan] = useState<Row>();
  return (
    <>
      <Header
        title="Membership dues"
        description="Effective-dated fee schedules and repeat-safe billing. Issued dues keep their original terms."
        action={
          <button className="op-primary" onClick={() => setForm("plan")}>
            New fee schedule
          </button>
        }
      />
      <Table
        rows={q.data ?? []}
        cols={[
          ["name", "Plan"],
          ["purpose", "Purpose"],
          ["effectiveFrom", "Effective from"],
          ["effectiveTo", "Effective to"],
          ["months", "Cycle (months)"],
          [
            "actions",
            "Billing",
            (r) => (
              <div className="fin-actions">
                <button
                  onClick={() => {
                    setPlan(r);
                    setForm("assess");
                  }}
                >
                  Assess member
                </button>
                <button
                  onClick={() => {
                    setPlan(r);
                    setForm("batch");
                  }}
                >
                  Queue bulk billing
                </button>
              </div>
            ),
          ],
        ]}
      />
      <section className="op-card">
        <h2>Durable billing jobs</h2>
        <Table
          rows={jobs.data ?? []}
          cols={[
            ["createdAt", "Created"],
            ["periodStart", "Period"],
            ["processed", "Processed"],
            ["status", "Status", (r) => <Badge>{r.status}</Badge>],
            [
              "error",
              "Review",
              (r) => (
                <>
                  {r.error}
                  {r.status === "Failed" && (
                    <button
                      onClick={() =>
                        a
                          .run("/finance/jobs/" + r.id + "/retry")
                          .catch(() => {})
                      }
                    >
                      Retry safely
                    </button>
                  )}
                </>
              ),
            ],
          ]}
        />
      </section>
      {form && opts.data && (
        <Form
          title={
            form === "plan"
              ? "Define fee schedule"
              : form === "batch"
                ? "Queue dues generation"
                : "Assess membership dues"
          }
          close={() => setForm("")}
          description="Monetary values are calculated by the server in decimal arithmetic. Waivers require posting authority and a recorded reason."
          fields={
            form === "plan"
              ? [
                  { key: "name", label: "Schedule name" },
                  { key: "chargeCode", label: "Stable charge definition code" },
                  {
                    key: "billingOrgId",
                    label: "Collection organization",
                    options: opts.data.organizations,
                  },
                  {
                    key: "categoryId",
                    label: "Membership category",
                    options: opts.data.categories,
                    required: false,
                  },
                  {
                    key: "purpose",
                    label: "Purpose",
                    options: [
                      "ClubDues",
                      "AssociationDues",
                      "DistrictDues",
                      "NationalDues",
                      "InternationalDues",
                      "JoiningFee",
                      "Renewal",
                      "Other",
                    ],
                  },
                  {
                    key: "effectiveFrom",
                    label: "Effective from",
                    type: "date",
                    value: today(),
                  },
                  {
                    key: "effectiveTo",
                    label: "Effective through",
                    type: "date",
                  },
                  {
                    key: "months",
                    label: "Cycle months",
                    type: "number",
                    value: 12,
                  },
                  {
                    key: "graceDays",
                    label: "Payment grace days",
                    type: "number",
                    value: 30,
                  },
                  {
                    key: "components",
                    label: "Fee components",
                    type: "components",
                    wide: true,
                  },
                  {
                    key: "prorate",
                    label: "Prorate joining within billing period",
                    type: "checkbox",
                  },
                  {
                    key: "once",
                    label: "Bill once (joining / life fee)",
                    type: "checkbox",
                  },
                  {
                    key: "autoBill",
                    label: "Enable scheduled billing",
                    type: "checkbox",
                  },
                ]
              : [
                  ...(form === "assess"
                    ? [
                        {
                          key: "membershipId",
                          label: "Membership",
                          options: opts.data.memberships,
                        },
                      ]
                    : []),
                  {
                    key: "startsOn",
                    label: "Billing period start (inclusive)",
                    type: "date",
                    value: today(),
                  },
                  {
                    key: "endsOn",
                    label: "Billing period end (exclusive)",
                    type: "date",
                  },
                  ...(form === "assess"
                    ? [
                        {
                          key: "discount",
                          label: "Approved concession / waiver",
                          type: "number",
                          value: 0,
                        },
                        {
                          key: "reason",
                          label: "Concession reason",
                          required: false,
                        },
                      ]
                    : []),
                ]
          }
          submit={(v) =>
            form === "plan"
              ? a.run(path + "/dues-plans", {
                  ...v,
                  months: Number(v.months),
                  graceDays: Number(v.graceDays),
                  components: JSON.parse(v.components),
                })
              : a.run(
                  "/finance/dues-plans/" +
                    plan?.id +
                    (form === "batch" ? "/batches" : "/assess"),
                  { ...v, reason: v.reason ?? "" },
                )
          }
        />
      )}
      <ErrorBox error={q.error ?? jobs.error} />
    </>
  );
}

function Payments() {
  const { path, a, currency } = useFinance();
  const q = useData(path + "/payments"),
    opts = useData(path + "/options"),
    banks = useData(path + "/bank-accounts"),
    invoices = useData(path + "/invoices");
  const [form, setForm] = useState(""),
    [record, setRecord] = useState<Row>(),
    [after, setAfter] = useState("");
  const list = useData(path + "/payments" + (after ? "?after=" + after : ""));
  return (
    <>
      <Header
        title="Payments & receipts"
        description="Record, independently verify, allocate and reconcile. Collection and bank settlement remain distinct."
        action={
          <button className="op-primary" onClick={() => setForm("record")}>
            Record collection
          </button>
        }
      />
      <Table
        rows={list.data ?? []}
        cols={[
          ["payer", "Payer"],
          ["reference", "Reference"],
          ["amount", "Gross", (r) => money(r.amount, currency)],
          ["unallocated", "Unallocated", (r) => money(r.unallocated, currency)],
          ["status", "Verification", (r) => <Badge>{r.status}</Badge>],
          [
            "settlementStatus",
            "Settlement",
            (r) => <Badge>{r.settlementStatus}</Badge>,
          ],
          [
            "actions",
            "Actions",
            (r) => (
              <div className="fin-actions">
                {r.status === "Pending" ? (
                  <button
                    onClick={() =>
                      a
                        .run("/finance/payments/" + r.id + "/verify")
                        .catch(() => {})
                    }
                  >
                    Verify independently
                  </button>
                ) : (
                  <>
                    <button
                      onClick={() => {
                        setRecord(r);
                        setForm("allocate");
                      }}
                    >
                      Allocate
                    </button>
                    <button
                      onClick={() => {
                        setRecord(r);
                        setForm("settle");
                      }}
                    >
                      Settle
                    </button>
                    <button
                      onClick={() => {
                        setRecord(r);
                        setForm("refund");
                      }}
                    >
                      Refund / chargeback
                    </button>
                    <a href={"/api/v1/finance/payments/" + r.id + "/receipt"}>
                      Receipt
                    </a>
                  </>
                )}
              </div>
            ),
          ],
        ]}
      />
      <PageButtons rows={list.data} setAfter={setAfter} />
      {form && opts.data && (
        <Form
          title={
            form === "record"
              ? "Record pending collection"
              : form === "allocate"
                ? "Allocate verified funds"
                : form === "settle"
                  ? "Record bank settlement"
                  : "Record completed refund / chargeback"
          }
          close={() => setForm("")}
          description={
            form === "refund"
              ? "Record evidence of a completed bank/provider refund. This action posts a correction; it does not send money through the gateway."
              : form === "settle"
                ? "Only enter a settlement after checking bank/provider evidence. Gateway clearing is not a bank balance."
                : "The server checks payer, entity, currency, balance and independent verification."
          }
          fields={
            form === "record"
              ? [
                  {
                    key: "personId",
                    label: "Member",
                    options: opts.data.people,
                    required: false,
                  },
                  { key: "payer", label: "Payer name" },
                  { key: "reference", label: "Unique payment reference" },
                  {
                    key: "method",
                    label: "Method",
                    options: ["BankTransfer", "Cash", "Cheque", "UPI"],
                  },
                  { key: "amount", label: "Amount received", type: "number" },
                  {
                    key: "date",
                    label: "Transaction date",
                    type: "date",
                    value: today(),
                  },
                  {
                    key: "evidence",
                    label: "Evidence reference",
                    type: "textarea",
                  },
                ]
              : form === "allocate"
                ? [
                    {
                      key: "invoiceId",
                      label: "Invoice for the same payer",
                      options: invoices.data
                        ?.filter((i: Row) => i.personId === record?.personId)
                        .map((i: Row) => ({
                          id: i.id,
                          name: (i.number || "Draft") + " · " + i.customer,
                        })),
                    },
                    {
                      key: "amount",
                      label: "Amount to allocate",
                      type: "number",
                    },
                  ]
                : form === "settle"
                  ? [
                      {
                        key: "bankAccountId",
                        label: "Bank / cash account",
                        options: banks.data,
                      },
                      {
                        key: "date",
                        label: "Settlement date",
                        type: "date",
                        value: today(),
                      },
                      {
                        key: "fee",
                        label: "Provider fee",
                        type: "number",
                        value: 0,
                      },
                      {
                        key: "reference",
                        label: "Settlement evidence reference",
                      },
                    ]
                  : [
                      {
                        key: "invoiceId",
                        label:
                          "Allocated invoice (leave blank for unallocated refund)",
                        options: invoices.data
                          ?.filter((i: Row) => i.personId === record?.personId)
                          .map((i: Row) => ({
                            id: i.id,
                            name: i.number || "Draft",
                          })),
                        required: false,
                      },
                      { key: "amount", label: "Amount", type: "number" },
                      {
                        key: "kind",
                        label: "Correction kind",
                        options: ["Refund", "Chargeback"],
                      },
                      {
                        key: "reference",
                        label: "Completed refund / bank reference",
                      },
                      { key: "reason", label: "Reason", type: "textarea" },
                    ]
          }
          submit={(v) =>
            a.run(
              form === "record"
                ? path + "/payments"
                : "/finance/payments/" +
                    record?.id +
                    "/" +
                    (form === "refund" ? "refunds" : form),
              { ...v, currency, key: key() },
            )
          }
        />
      )}
      <ErrorBox error={q.error ?? list.error} />
    </>
  );
}
function Reconciliation() {
  const { path, a, currency } = useFinance();
  const q = useData(path + "/reconciliation"),
    banks = useData(path + "/bank-accounts"),
    payments = useData(path + "/payments");
  const [form, setForm] = useState(""),
    [row, setRow] = useState<Row>();
  return (
    <>
      <Header
        title="Bank reconciliation"
        description="Match imported evidence to settled collections without posting the bank transaction twice."
        action={
          <button className="op-primary" onClick={() => setForm("import")}>
            Import statement rows
          </button>
        }
      />
      <Table
        rows={q.data ?? []}
        cols={[
          ["date", "Date"],
          ["reference", "Reference"],
          ["amount", "Amount", (r) => money(r.amount, currency)],
          ["status", "State", (r) => <Badge>{r.status}</Badge>],
          [
            "action",
            "Matching",
            (r) =>
              r.status === "Unmatched" ? (
                <button
                  onClick={() => {
                    setRow(r);
                    setForm("match");
                  }}
                >
                  Match payment
                </button>
              ) : (
                "Reconciled"
              ),
          ],
        ]}
      />
      {form && (
        <Form
          title={
            form === "match"
              ? "Match statement transaction"
              : "Import bank statement"
          }
          close={() => setForm("")}
          description="Import up to 500 rows as JSON objects: date (YYYY-MM-DD), amount, reference, key. Keys prevent duplicate import. Negative rows are retained for review."
          fields={
            form === "match"
              ? [
                  {
                    key: "paymentId",
                    label: "Settled payment",
                    options: payments.data
                      ?.filter((p: Row) => p.settlementStatus === "Settled")
                      .map((p: Row) => ({
                        id: p.id,
                        name: p.reference + " · " + p.payer,
                      })),
                  },
                ]
              : [
                  {
                    key: "bankAccountId",
                    label: "Bank account",
                    options: banks.data,
                  },
                  {
                    key: "rows",
                    label: "Statement rows (JSON)",
                    type: "textarea",
                    wide: true,
                    value:
                      '[{"date":"' +
                      today() +
                      '","amount":"100.00","reference":"BANK-REFERENCE","key":"unique-import-row"}]',
                  },
                ]
          }
          submit={(v) =>
            form === "match"
              ? a.run("/finance/reconciliation/" + row?.id + "/match", v)
              : a.run(path + "/reconciliation/import", {
                  ...v,
                  rows: JSON.parse(v.rows),
                })
          }
        />
      )}
      <ErrorBox error={q.error} />
    </>
  );
}
function Accounting() {
  const { path, a, currency } = useFinance();
  const accounts = useData(path + "/accounts"),
    journals = useData(path + "/journals");
  const [form, setForm] = useState(""),
    [detail, setDetail] = useState<Row>();
  return (
    <>
      <Header
        title="Accounting"
        description="Balanced, immutable journals in open accounting periods. Corrections create reversing entries."
        action={
          <div className="fin-actions">
            <button onClick={() => setForm("account")}>Add account</button>
            <button className="op-primary" onClick={() => setForm("journal")}>
              Post journal
            </button>
          </div>
        }
      />
      <div className="fin-two">
        <section className="op-card">
          <h2>Chart of accounts</h2>
          <Table
            rows={accounts.data ?? []}
            cols={[
              ["code", "Code"],
              ["name", "Account"],
              ["kind", "Classification"],
            ]}
          />
        </section>
        <section className="op-card">
          <h2>Posted journals</h2>
          <Table
            rows={journals.data ?? []}
            onRow={async (r) => {
              try {
                setDetail(await api("/finance/journals/" + r.id));
              } catch {}
            }}
            cols={[
              ["date", "Date"],
              ["narration", "Narration"],
              [
                "action",
                "Correction",
                (r) =>
                  r.source.startsWith("manual:") ? (
                    <button
                      onClick={() => {
                        setDetail({ journal: r });
                        setForm("reverse");
                      }}
                    >
                      Reverse
                    </button>
                  ) : (
                    <span className="op-muted">Source controlled</span>
                  ),
              ],
            ]}
          />
        </section>
      </div>
      {detail?.lines && (
        <section className="op-card">
          <h2>{detail.journal.narration}</h2>
          <Table
            rows={detail.lines}
            cols={[
              ["code", "Account"],
              ["name", "Name"],
              ["debit", "Debit", (r) => money(r.debit, currency)],
              ["credit", "Credit", (r) => money(r.credit, currency)],
            ]}
          />
        </section>
      )}
      {form && (
        <Form
          title={
            form === "account"
              ? "Add ledger account"
              : form === "reverse"
                ? "Reverse manual journal"
                : "Post balanced journal"
          }
          close={() => setForm("")}
          description="A posted journal cannot be edited or deleted. Posting fails if the period is closed or debits and credits differ."
          fields={
            form === "account"
              ? [
                  { key: "code", label: "Account code (4–10 digits)" },
                  { key: "name", label: "Name" },
                  {
                    key: "kind",
                    label: "Classification",
                    options: [
                      "Asset",
                      "Liability",
                      "Equity",
                      "Income",
                      "Expense",
                    ],
                  },
                ]
              : form === "reverse"
                ? [
                    {
                      key: "reason",
                      label: "Reversal reason",
                      type: "textarea",
                    },
                  ]
                : [
                    {
                      key: "date",
                      label: "Posting date",
                      type: "date",
                      value: today(),
                    },
                    { key: "narration", label: "Narration" },
                    {
                      key: "lines",
                      label: "Journal lines (JSON: account, debit, credit)",
                      type: "textarea",
                      wide: true,
                      value:
                        '[{"account":"1010","debit":"100.00","credit":"0"},{"account":"3000","debit":"0","credit":"100.00"}]',
                    },
                  ]
          }
          submit={(v) =>
            form === "reverse"
              ? a.run("/finance/journals/" + detail?.journal.id + "/reverse", v)
              : a.run(
                  path + (form === "account" ? "/accounts" : "/journals"),
                  form === "journal"
                    ? { ...v, key: key(), lines: JSON.parse(v.lines) }
                    : v,
                )
          }
        />
      )}
      <ErrorBox error={accounts.error ?? journals.error} />
    </>
  );
}
function Budgets() {
  const { path, a, currency } = useFinance();
  const q = useData(path + "/budgets"),
    opts = useData(path + "/options");
  const [open, setOpen] = useState(false),
    [revision, setRevision] = useState<Row>(),
    [detail, setDetail] = useState<Row>();
  return (
    <>
      <Header
        title="Budgets & commitments"
        description="Preserve approved revisions. Approval turns an expense commitment into an actual accrual."
        action={
          <button
            className="op-primary"
            onClick={() => {
              setRevision(undefined);
              setOpen(true);
            }}
          >
            Create budget
          </button>
        }
      />
      <Table
        rows={q.data ?? []}
        cols={[
          ["name", "Budget"],
          ["revision", "Revision"],
          ["amount", "Approved limit", (r) => money(r.amount, currency)],
          ["status", "Review", (r) => <Badge>{r.status}</Badge>],
          [
            "action",
            "Actions",
            (r) => (
              <div className="fin-actions">
                {r.status === "Draft" && (
                  <button
                    onClick={() =>
                      a
                        .run("/finance/budgets/" + r.id + "/submit")
                        .catch(() => {})
                    }
                  >
                    Submit for review
                  </button>
                )}
                {r.status === "Approved" && (
                  <button
                    onClick={() => {
                      setRevision(r);
                      setOpen(true);
                    }}
                  >
                    Create revision
                  </button>
                )}
                <button
                  onClick={async () => {
                    try {
                      setDetail(
                        await api(path + "/budgets/" + r.id + "/actual"),
                      );
                    } catch {}
                  }}
                >
                  Budget vs actual
                </button>
              </div>
            ),
          ],
        ]}
      />
      {detail && (
        <section className="op-card">
          <h2>{detail.budget.name} · utilization</h2>
          <div className="fin-stats">
            {[
              ["Commitments", detail.committed],
              ["Actual accrued", detail.actual],
              ["Available", detail.available],
              ["Variance", detail.variance],
            ].map(([n, v]) => (
              <div key={String(n)}>
                <p>{n}</p>
                <h3>{money(v, currency)}</h3>
              </div>
            ))}
          </div>
          <p className="op-muted">{detail.formula}</p>
        </section>
      )}
      {open && opts.data && (
        <Form
          title={revision ? "Revise approved budget" : "Create budget"}
          close={() => setOpen(false)}
          fields={[
            { key: "name", label: "Budget name", value: revision?.name },
            {
              key: "kind",
              label: "Budget type",
              options: [
                "Annual",
                "District",
                "National",
                "Committee",
                "Event",
                "Project",
              ],
              value: revision?.kind,
            },
            {
              key: "startsOn",
              label: "Period starts",
              type: "date",
              value: revision?.startsOn ?? today(),
            },
            {
              key: "endsOn",
              label: "Period ends",
              type: "date",
              value: revision?.endsOn,
            },
            {
              key: "projectId",
              label: "Project",
              options: opts.data.projects,
              required: false,
              value: revision?.projectId,
            },
            {
              key: "eventId",
              label: "Event",
              options: opts.data.events,
              required: false,
              value: revision?.eventId,
            },
            {
              key: "lines",
              label: "Budget allocations",
              type: "components",
              wide: true,
            },
          ]}
          submit={(v) =>
            a.run(path + "/budgets", {
              ...v,
              replacesId: revision?.id ?? null,
              lines: JSON.parse(v.lines),
            })
          }
        />
      )}
      <FinancialReviews />
      <p className="fin-policy">
        Financial reviews use the existing Governance approval workflow.{" "}
        <Link to="/app/governance">Open approvals</Link>
      </p>
      <ErrorBox error={q.error} />
    </>
  );
}
function Expenses() {
  const { path, a, currency } = useFinance();
  const q = useData(path + "/expenses"),
    opts = useData(path + "/options"),
    budgets = useData(path + "/budgets");
  const [form, setForm] = useState(""),
    [record, setRecord] = useState<Row>();
  return (
    <>
      <Header
        title="Expenses & reimbursements"
        description="Evidence-backed claims, independent approval, and separate payment records."
        action={
          <button className="op-primary" onClick={() => setForm("claim")}>
            Submit an expense
          </button>
        }
      />
      <Table
        rows={q.data ?? []}
        cols={[
          ["payee", "Payee"],
          ["description", "Claim"],
          ["amount", "Accrued", (r) => money(r.amount, currency)],
          ["paid", "Paid", (r) => money(r.paid, currency)],
          ["status", "Review", (r) => <Badge>{r.status}</Badge>],
          [
            "action",
            "Actions",
            (r) => (
              <div className="fin-actions">
                {r.status === "Draft" && (
                  <button
                    onClick={() =>
                      a
                        .run("/finance/expenses/" + r.id + "/submit")
                        .catch(() => {})
                    }
                  >
                    Request approval
                  </button>
                )}
                {r.status === "Approved" && (
                  <button
                    onClick={() => {
                      setRecord(r);
                      setForm("pay");
                    }}
                  >
                    Record reimbursement
                  </button>
                )}
                {r.requestId && (
                  <Link to={"/app/governance?request=" + r.requestId}>
                    Review history
                  </Link>
                )}
              </div>
            ),
          ],
        ]}
      />
      {form && opts.data && (
        <Form
          title={
            form === "claim"
              ? "Create expense claim"
              : "Record completed reimbursement"
          }
          close={() => setForm("")}
          description="Upload evidence in the existing document vault first. Approval records a liability; reimbursement records a separately evidenced bank payment. Self approval and self disbursement are blocked."
          fields={
            form === "claim"
              ? [
                  {
                    key: "personId",
                    label: "Claimant",
                    options: opts.data.people,
                  },
                  { key: "payee", label: "Vendor / payee" },
                  { key: "description", label: "Claim description" },
                  {
                    key: "category",
                    label: "Category",
                    options: [
                      "Administration",
                      "Meeting",
                      "Event",
                      "Project",
                      "Travel",
                      "Venue",
                      "Supplies",
                      "Technology",
                      "Other",
                    ],
                  },
                  {
                    key: "date",
                    label: "Expense date",
                    type: "date",
                    value: today(),
                  },
                  {
                    key: "lines",
                    label: "Expense lines (tax inclusive)",
                    type: "components",
                    wide: true,
                  },
                  { key: "evidenceId", label: "Supporting vault document ID" },
                  {
                    key: "projectId",
                    label: "Project",
                    options: opts.data.projects,
                    required: false,
                  },
                  {
                    key: "eventId",
                    label: "Event",
                    options: opts.data.events,
                    required: false,
                  },
                  {
                    key: "budgetId",
                    label: "Approved budget",
                    options: budgets.data?.filter(
                      (b: Row) => b.status === "Approved",
                    ),
                    required: false,
                  },
                ]
              : [
                  {
                    key: "amount",
                    label: "Amount actually paid",
                    type: "number",
                  },
                  {
                    key: "reference",
                    label: "Completed bank transfer reference",
                  },
                  {
                    key: "date",
                    label: "Payment date",
                    type: "date",
                    value: today(),
                  },
                ]
          }
          submit={(v) =>
            form === "claim"
              ? a.run(path + "/expenses", {
                  ...v,
                  lines: JSON.parse(v.lines),
                  evidence: [v.evidenceId],
                })
              : a.run("/finance/expenses/" + record?.id + "/reimburse", v)
          }
        />
      )}
      <FinancialReviews />
      <ErrorBox error={q.error} />
    </>
  );
}
function Funding() {
  const { path, a, currency } = useFinance();
  const q = useData(path + "/pledges"),
    opts = useData(path + "/options");
  const [open, setOpen] = useState(false);
  return (
    <>
      <Header
        title="Donations & sponsorships"
        description="A pledge is an intention. An invoice is a receivable. A verified payment is collection."
        action={
          <button className="op-primary" onClick={() => setOpen(true)}>
            Record pledge
          </button>
        }
      />
      <Table
        rows={q.data ?? []}
        cols={[
          ["contributor", "Contributor"],
          ["kind", "Type"],
          ["amount", "Pledged", (r) => money(r.amount, currency)],
          ["status", "Stage", (r) => <Badge>{r.status}</Badge>],
          [
            "action",
            "Receivable",
            (r) =>
              r.invoiceId ? (
                <Link to={root + "/invoices/" + r.invoiceId}>
                  View central invoice
                </Link>
              ) : (
                <button
                  onClick={() =>
                    a
                      .run("/finance/pledges/" + r.id + "/invoice")
                      .catch(() => {})
                  }
                >
                  Recognize receivable & invoice
                </button>
              ),
          ],
        ]}
      />
      <p className="fin-policy">
        Donation receipts do not imply tax deductibility. The financial entity
        must configure and review its local tax treatment.
      </p>
      {open && opts.data && (
        <Form
          title="Record donation / sponsorship pledge"
          close={() => setOpen(false)}
          fields={[
            {
              key: "personId",
              label: "Existing member / individual",
              options: opts.data.people,
              required: false,
            },
            { key: "contributor", label: "Individual or organization name" },
            {
              key: "kind",
              label: "Funding type",
              options: ["Donation", "Sponsorship"],
            },
            { key: "description", label: "Commitment description" },
            { key: "amount", label: "Pledged amount", type: "number" },
            { key: "date", label: "Pledge date", type: "date", value: today() },
            {
              key: "projectId",
              label: "Beneficiary project",
              options: opts.data.projects,
              required: false,
            },
            {
              key: "eventId",
              label: "Beneficiary event",
              options: opts.data.events,
              required: false,
            },
          ]}
          submit={(v) => a.run(path + "/pledges", v)}
        />
      )}
      <ErrorBox error={q.error} />
    </>
  );
}
function Allocations() {
  const { path, a, currency } = useFinance();
  const q = useData(path + "/inter-entity");
  const [r, setR] = useState<Row>();
  return (
    <>
      <Header
        title="Inter-entity allocations"
        description="Entitlements, collections and bank settlements are separate records. No automatic fund transfer is initiated."
      />
      <Table
        rows={q.data?.entitlements ?? []}
        cols={[
          ["id", "Entitlement"],
          ["amount", "Entitled", (x) => money(x.amount, currency)],
          ["settled", "Settled", (x) => money(x.settled, currency)],
          [
            "action",
            "Settlement",
            (x) => (
              <button onClick={() => setR(x)}>Record completed transfer</button>
            ),
          ],
        ]}
      />
      <section className="op-card">
        <h2>Settlement history</h2>
        <Table
          rows={q.data?.transfers ?? []}
          cols={[
            ["date", "Date"],
            ["reference", "Bank reference"],
            ["amount", "Amount", (x) => money(x.amount, currency)],
            ["status", "State"],
          ]}
        />
      </section>
      {r && (
        <Form
          title="Record completed inter-entity settlement"
          close={() => setR(undefined)}
          description="Requires authority in both entities and full invoice collection. Posts both entity books atomically. Record evidence only after the real bank transfer is completed."
          fields={[
            { key: "amount", label: "Transferred amount", type: "number" },
            {
              key: "date",
              label: "Bank transfer date",
              type: "date",
              value: today(),
            },
            { key: "reference", label: "Completed transfer reference" },
          ]}
          submit={(v) => a.run("/finance/inter-entity/" + r.id + "/settle", v)}
        />
      )}
      <ErrorBox error={q.error} />
    </>
  );
}

const reports = [
  ["dues-register", "Membership dues register"],
  ["outstanding", "Outstanding dues"],
  ["ageing", "Ageing analysis"],
  ["collections", "Collections"],
  ["invoices", "Invoice register"],
  ["receipts", "Receipts register"],
  ["refunds", "Refunds"],
  ["reconciliation", "Payment reconciliation"],
  ["income-statement", "Income statement"],
  ["balance-sheet", "Balance sheet"],
  ["trial-balance", "Trial balance"],
  ["general-ledger", "General ledger"],
  ["account-ledger", "Account ledger"],
  ["bank-book", "Bank / cash book"],
  ["expenses", "Expense register"],
  ["budget-actual", "Budget vs actual"],
  ["event", "Event finance"],
  ["project", "Project financial summary"],
  ["donations", "Donations register"],
  ["inter-entity", "Inter-entity settlement"],
  ["audit", "Financial audit"],
  ["member-statement", "Member statement"],
];
function Reports() {
  const { path, currency } = useFinance();
  const [kind, setKind] = useState("trial-balance"),
    [from, setFrom] = useState(today().slice(0, 4) + "-01-01"),
    [to, setTo] = useState(today()),
    [account, setAccount] = useState(""),
    [after, setAfter] = useState("");
  const url =
    path +
    "/reports/" +
    kind +
    "?from=" +
    from +
    "&to=" +
    to +
    "&account=" +
    encodeURIComponent(account) +
    (after ? "&after=" + after : "");
  const q = useData(url);
  return (
    <>
      <Header
        title="Financial reports"
        description="Reconciled entity books, transparent formulas and native currency. Independent entity bank balances are never silently consolidated."
      />
      <section className="op-card fin-filters">
        <label>
          Report
          <select
            value={kind}
            onChange={(e) => {
              setKind(e.target.value);
              setAfter("");
            }}
          >
            {reports.map(([id, name]) => (
              <option key={id} value={id}>
                {name}
              </option>
            ))}
          </select>
        </label>
        <label>
          From
          <input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
          />
        </label>
        <label>
          Through
          <input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
          />
        </label>
        <label>
          Account code
          <input
            value={account}
            onChange={(e) => setAccount(e.target.value)}
            placeholder="All accounts"
          />
        </label>
        <a href={"/api/v1" + url + "&format=csv"}>Export accounts CSV</a>
        <a
          href={"/api/v1" + url + "&format=pdf"}
          target="_blank"
          rel="noreferrer"
        >
          Accounts PDF
        </a>
      </section>
      {q.data && (
        <>
          <div className="fin-stats">
            {[
              ["Income", q.data.income],
              ["Expenses", q.data.expense],
              ["Surplus", q.data.surplus],
              ["Trial difference", q.data.trialDifference],
            ].map(([name, v]) => (
              <section className="op-card" key={String(name)}>
                <p>{name}</p>
                <h2>{money(v, currency)}</h2>
              </section>
            ))}
          </div>
          <section className="op-card">
            <h2>Account balances through {to}</h2>
            <Table
              rows={q.data.accounts}
              cols={[
                ["code", "Code"],
                ["name", "Account"],
                ["kind", "Type"],
                ["debit", "Debit", (r) => money(r.debit, currency)],
                ["credit", "Credit", (r) => money(r.credit, currency)],
                ["balance", "Balance", (r) => money(r.balance, currency)],
              ]}
            />
          </section>
          {kind === "ageing" && (
            <Table
              rows={q.data.ageing}
              cols={[
                ["bucket", "Age"],
                ["amount", "Outstanding", (r) => money(r.amount, currency)],
              ]}
            />
          )}
          <section className="op-card">
            <h2>Report records</h2>
            <Table
              rows={q.data.records}
              cols={recordColumns(q.data.records, currency)}
            />
          </section>
          <section className="op-card">
            <h2>General ledger activity</h2>
            <Table
              rows={q.data.entries}
              cols={[
                ["date", "Date"],
                ["code", "Account"],
                ["narration", "Narration"],
                ["debit", "Debit", (r) => money(r.debit, currency)],
                ["credit", "Credit", (r) => money(r.credit, currency)],
              ]}
            />
            {q.data.next && (
              <button onClick={() => setAfter(q.data.next)}>
                Next ledger entries →
              </button>
            )}
          </section>
          <p className="fin-policy">
            {q.data.notes} Exports currently contain the account summary;
            detailed records are available in the paged workspace/API.
          </p>
        </>
      )}
      <ErrorBox error={q.error} />
    </>
  );
}
function recordColumns(
  rows: Row[],
  currency: string,
): [string, string, ((r: Row) => any)?][] {
  if (!rows?.length) return [["id", "Record"]];
  const preferred = [
    "number",
    "customer",
    "receiptNumber",
    "payer",
    "reference",
    "description",
    "contributor",
    "name",
    "date",
    "status",
    "amount",
    "total",
    "paid",
    "credited",
    "outstanding",
    "settled",
    "action",
    "detail",
  ];
  const keys = preferred.filter((k) => k in rows[0]).slice(0, 7);
  return (keys.length ? keys : ["id"]).map((k) => [
    k,
    k.replace(/([A-Z])/g, " $1"),
    ["amount", "total", "paid", "credited", "outstanding", "settled"].includes(
      k,
    )
      ? (r) => money(r[k], currency)
      : undefined,
  ]);
}
function Sources() {
  const { path, a, currency } = useFinance();
  const opts = useData(path + "/options"),
    prices = useData(path + "/event-prices");
  const [form, setForm] = useState(false),
    [source, setSource] = useState<Row>(),
    [type, setType] = useState("project");
  const report = useData(
    path +
      "/reports/" +
      type +
      "?" +
      (type === "project" ? "projectId=" : "eventId=") +
      (source?.id ?? ""),
    !!source,
  );
  return (
    <>
      <Header
        title="Event & project finance"
        description="Central invoices, accrued expenses and verified collections. Service estimates and uncollected pledges stay separate."
        action={
          <button className="op-primary" onClick={() => setForm(true)}>
            Configure paid event
          </button>
        }
      />
      <section className="op-card">
        <div className="fin-filters">
          <select
            aria-label="Source type"
            value={type}
            onChange={(e) => {
              setType(e.target.value);
              setSource(undefined);
            }}
          >
            <option value="project">Service project</option>
            <option value="event">Event</option>
          </select>
          <select
            aria-label="Select source"
            value={source?.id ?? ""}
            onChange={(e) =>
              setSource(
                (type === "project"
                  ? opts.data?.projects
                  : opts.data?.events
                )?.find((x: Row) => x.id === e.target.value),
              )
            }
          >
            <option value="">Select source</option>
            {(type === "project"
              ? opts.data?.projects
              : opts.data?.events
            )?.map((x: Row) => (
              <option key={x.id} value={x.id}>
                {x.name}
              </option>
            ))}
          </select>
        </div>
        {report.data && (
          <>
            <h2>{source?.name}</h2>
            <p>
              Accrued income {money(report.data.income, currency)} · Expenses{" "}
              {money(report.data.expense, currency)} · Surplus{" "}
              {money(report.data.surplus, currency)}
            </p>
            <Table
              rows={report.data.entries}
              cols={[
                ["date", "Date"],
                ["narration", "Source"],
                ["debit", "Debit", (r) => money(r.debit, currency)],
                ["credit", "Credit", (r) => money(r.credit, currency)],
              ]}
            />
          </>
        )}
      </section>
      <section className="op-card">
        <h2>Configured event fees</h2>
        <Table
          rows={prices.data ?? []}
          cols={[
            ["eventId", "Event reference"],
            ["category", "Category"],
            ["amount", "Price", (r) => money(r.amount, currency)],
          ]}
        />
        <p className="op-muted">
          Paid registration starts in Events and remains pending until a central
          invoice is fully allocated. Paid events currently require no
          companions or registration approval. A provider or independently
          reconciled manual alternative must be available.
        </p>
      </section>
      {form && (
        <Form
          title="Configure event collection"
          close={() => setForm(false)}
          fields={[
            { key: "eventId", label: "Event", options: opts.data?.events },
            {
              key: "category",
              label: "Price category",
              options: ["Member", "Guest"],
            },
            { key: "amount", label: "Registration fee", type: "number" },
          ]}
          submit={(v) => a.run(path + "/event-prices", v)}
        />
      )}
      <ErrorBox error={report.error ?? prices.error} />
    </>
  );
}
function Settings() {
  const { path, a, e, currency } = useFinance();
  const q = useData(path + "/settings"),
    banks = useData(path + "/bank-accounts");
  const [form, setForm] = useState(""),
    [period, setPeriod] = useState<Row>();
  return (
    <>
      <Header
        title="Financial settings"
        description="Separate financial authority, periods, banking and collection configuration."
      />
      <section className="op-card">
        <h2>{e.name}</h2>
        <p>
          {e.country} · {currency} · {e.decimals} decimal places · Accrual
        </p>
        <p>{e.collectionAuthority}</p>
        <div className="fin-actions">
          <button onClick={() => setForm("grant")}>Grant entity access</button>
          <button onClick={() => setForm("organization")}>
            Authorize collection organization
          </button>
          <button onClick={() => setForm("period")}>
            Open accounting period
          </button>
          <button onClick={() => setForm("bank")}>Add bank account</button>
          <button onClick={() => setForm("gateway")}>Bind gateway alias</button>
          <button onClick={() => setForm("rate")}>Record exchange rate</button>
        </div>
      </section>
      {q.data && (
        <>
          <section className="op-card">
            <h2>Accounting periods</h2>
            <Table
              rows={q.data.periods}
              cols={[
                ["name", "Period"],
                ["startsOn", "Starts"],
                ["endsOn", "Ends"],
                [
                  "closed",
                  "Status",
                  (r) => <Badge>{r.closed ? "Closed" : "Open"}</Badge>,
                ],
                [
                  "action",
                  "Control",
                  (r) => (
                    <button
                      onClick={() => {
                        setPeriod(r);
                        setForm(r.closed ? "reopen" : "close");
                      }}
                    >
                      {r.closed ? "Reopen with audit" : "Close period"}
                    </button>
                  ),
                ],
              ]}
            />
          </section>
          <div className="fin-two">
            <section className="op-card">
              <h2>Explicit entity access</h2>
              <Table
                rows={q.data.grants}
                cols={[
                  ["userId", "Association user"],
                  ["role", "Financial role"],
                  [
                    "action",
                    "Revoke",
                    (r) => (
                      <button
                        onClick={() =>
                          a
                            .run(
                              path + "/grants/" + r.userId,
                              undefined,
                              "DELETE",
                            )
                            .catch(() => {})
                        }
                      >
                        Revoke grant
                      </button>
                    ),
                  ],
                ]}
              />
            </section>
            <section className="op-card">
              <h2>Protected bank accounts</h2>
              <Table
                rows={banks.data ?? []}
                cols={[
                  ["name", "Account"],
                  ["maskedNumber", "Number"],
                  ["accountCode", "Ledger"],
                ]}
              />
            </section>
          </div>
        </>
      )}
      {form && (
        <Form
          title={
            {
              grant: "Grant entity authority",
              organization: "Authorize collection",
              period: "Create accounting period",
              bank: "Add protected bank account",
              gateway: "Bind server gateway configuration",
              rate: "Record historical reporting rate",
              close: "Close accounting period",
              reopen: "Reopen accounting period",
            }[form] ?? form
          }
          close={() => setForm("")}
          description={
            form === "gateway"
              ? "Enter only a server-configured alias. Secrets never enter the browser. The alias must explicitly bind this entity. Only Stripe is currently implemented."
              : form === "grant"
                ? "The user also needs corresponding scoped application finance permissions. Entity authority does not grant general administration."
                : ""
          }
          fields={
            form === "grant"
              ? [
                  { key: "userId", label: "Existing association user ID" },
                  {
                    key: "role",
                    label: "Financial authority",
                    options: [
                      "Reader",
                      "Billing",
                      "Treasurer",
                      "Reviewer",
                      "Owner",
                    ],
                  },
                ]
              : form === "organization"
                ? [
                    { key: "orgId", label: "Organization ID" },
                    {
                      key: "authority",
                      label: "Explicit authorized collection arrangement",
                      type: "textarea",
                    },
                  ]
                : form === "period"
                  ? [
                      { key: "name", label: "Period name" },
                      {
                        key: "startsOn",
                        label: "Starts",
                        type: "date",
                        value: today().slice(0, 4) + "-01-01",
                      },
                      {
                        key: "endsOn",
                        label: "Ends",
                        type: "date",
                        value: today().slice(0, 4) + "-12-31",
                      },
                    ]
                  : form === "bank"
                    ? [
                        { key: "name", label: "Bank / cash name" },
                        { key: "accountNumber", label: "Bank account number" },
                        {
                          key: "routingDetails",
                          label: "Routing details",
                          type: "textarea",
                        },
                        {
                          key: "accountCode",
                          label: "Cash / bank ledger code",
                          value: "1010",
                        },
                      ]
                    : form === "rate"
                      ? [
                          { key: "currency", label: "Reporting currency" },
                          {
                            key: "rate",
                            label: "Native to reporting rate",
                            type: "number",
                          },
                          {
                            key: "date",
                            label: "Rate date",
                            type: "date",
                            value: today(),
                          },
                          { key: "source", label: "Rate source" },
                        ]
                      : [
                          {
                            key: "reason",
                            label:
                              form === "gateway"
                                ? "Configured gateway alias"
                                : "Reason",
                            type: form === "gateway" ? "text" : "textarea",
                          },
                        ]
          }
          submit={(v) =>
            a.run(
              path +
                (form === "grant"
                  ? "/grants"
                  : form === "organization"
                    ? "/organizations"
                    : form === "period"
                      ? "/periods"
                      : form === "bank"
                        ? "/bank-accounts"
                        : form === "rate"
                          ? "/exchange-rates"
                          : form === "gateway"
                            ? "/gateway"
                            : "/periods/" + period?.id + "/" + form),
              v,
              form === "gateway" ? "PUT" : "POST",
            )
          }
        />
      )}
      <ErrorBox error={q.error ?? banks.error} />
      <Audit />
    </>
  );
}
function Audit() {
  const { path } = useFinance();
  const q = useData(path + "/audit");
  return (
    <section className="op-card">
      <h2>Financial audit trail</h2>
      <Table
        rows={q.data ?? []}
        cols={[
          ["at", "Time"],
          ["action", "Action"],
          ["detail", "Details"],
        ]}
      />
    </section>
  );
}

function LineEditor({ name, invoice }: any) {
  const { path } = useFinance();
  const accounts = useData(path + "/accounts", !!path);
  const entities = useData("/finance/entities");
  const fresh = () =>
    invoice
      ? {
          description: "",
          quantity: "1",
          rate: "",
          discount: "0",
          taxPercent: "0",
          accountCode: "4000",
          beneficiaryEntityId: null,
        }
      : {
          description: "",
          amount: "",
          accountCode: "4000",
          taxPercent: "0",
          beneficiaryEntityId: null,
        };
  const [lines, setLines] = useState<Row[]>([fresh()]);
  function update(n: number, k: string, v: any) {
    setLines((old) => old.map((r, i) => (i === n ? { ...r, [k]: v } : r)));
  }
  return (
    <div className="fin-lines">
      <input name={name} type="hidden" value={JSON.stringify(lines)} />
      {lines.map((line, n) => (
        <section className="fin-line" key={n}>
          <label>
            Description
            <input
              required
              aria-label={"Line " + (n + 1) + " description"}
              value={line.description}
              onChange={(e) => update(n, "description", e.target.value)}
            />
          </label>
          {(invoice
            ? ["quantity", "rate", "discount", "taxPercent"]
            : name === "components"
              ? ["amount", "taxPercent"]
              : ["amount"]
          ).map((k) => (
            <label key={k}>
              {k === "taxPercent" ? "Tax %" : k}
              <input
                required
                type="number"
                step="any"
                min="0"
                aria-label={"Line " + (n + 1) + " " + k}
                value={line[k]}
                onChange={(e) => update(n, k, e.target.value)}
              />
            </label>
          ))}
          {(invoice || name === "components") && (
            <>
              <label>
                Income account
                <select
                  value={line.accountCode}
                  onChange={(e) => update(n, "accountCode", e.target.value)}
                >
                  {accounts.data
                    ?.filter((x: Row) => x.kind === "Income")
                    .map((x: Row) => (
                      <option key={x.id} value={x.code}>
                        {x.code} · {x.name}
                      </option>
                    ))}
                </select>
              </label>
              <label>
                Beneficiary
                <select
                  value={line.beneficiaryEntityId ?? ""}
                  onChange={(e) =>
                    update(n, "beneficiaryEntityId", e.target.value || null)
                  }
                >
                  <option value="">Collecting entity</option>
                  {entities.data?.map((x: Row) => (
                    <option key={x.id} value={x.id}>
                      {x.name}
                    </option>
                  ))}
                </select>
              </label>
            </>
          )}
          {lines.length > 1 && (
            <button
              type="button"
              onClick={() => setLines((old) => old.filter((_, i) => i !== n))}
            >
              Remove line
            </button>
          )}
        </section>
      ))}
      <button
        type="button"
        onClick={() => setLines((old) => [...old, fresh()])}
      >
        + Add line
      </button>
      <p className="op-muted">
        Final totals are calculated and rounded by the server. Entitlements do
        not transfer funds.
      </p>
    </div>
  );
}
function FinancialReviews() {
  const { path, a, currency } = useFinance();
  const q = useData(path + "/reviews");
  const [record, setRecord] = useState<Row>();
  return (
    <section className="op-card">
      <h2>Financial approvals</h2>
      <Table
        rows={q.data?.requests ?? []}
        cols={[
          ["title", "Proposal"],
          ["kind", "Type"],
          ["status", "Stage", (r) => <Badge>{r.status}</Badge>],
          [
            "actions",
            "Review",
            (r) => (
              <button onClick={() => setRecord(r)}>Review proposal</button>
            ),
          ],
        ]}
      />
      {record && (
        <Form
          title="Independent financial review"
          details={(() => {
            const d = [
              ...(q.data?.expenses ?? []),
              ...(q.data?.budgets ?? []),
            ].find((x: Row) => x.requestId === record.id);
            return d ? (
              <section>
                <Table
                  rows={JSON.parse(d.lines)}
                  cols={[
                    ["description", "Description"],
                    ["amount", "Amount", (r) => money(r.amount, currency)],
                  ]}
                />
                {d.evidence &&
                  JSON.parse(d.evidence).map((id: string) => (
                    <p key={id}>
                      <a
                        href={
                          "/api/v1/documents/" + id + "/download?preview=true"
                        }
                        target="_blank"
                        rel="noreferrer"
                      >
                        Review protected receipt evidence
                      </a>
                    </p>
                  ))}
              </section>
            ) : null;
          })()}
          close={() => setRecord(undefined)}
          description={(() => {
            const detail = [
              ...(q.data?.expenses ?? []),
              ...(q.data?.budgets ?? []),
            ].find((x: Row) => x.requestId === record.id);
            return detail
              ? (detail.description ?? detail.name) +
                  " · " +
                  money(detail.amount, currency) +
                  " · Supporting evidence must be shared through the original document vault."
              : record.status;
          })()}
          fields={[
            {
              key: "decision",
              label: "Decision",
              options: ["Approve", "Reject", "RequestChanges"],
            },
            { key: "comment", label: "Review explanation", type: "textarea" },
          ]}
          submit={(v) =>
            a.run("/governance/requests/" + record.id + "/decisions", {
              ...v,
              version: record.version,
            })
          }
        />
      )}
      <ErrorBox error={q.error} />
    </section>
  );
}
