import { useState } from "react";
import { Link } from "react-router-dom";
import { useWorkspace } from "../App";
import { Field, ModalForm, useAction, useGov } from "../GovernanceUI";
import { comm } from "./Communications";
import { OrgSelect } from "./CommunicationCampaigns";

const presets = {
  Birthday: {
    name: "Birthday greetings",
    subject: "Happy birthday, {{member_name}}!",
    body: "Dear {{member_name}}, happy birthday! Wishing you a wonderful year of happiness, good health, and success. Warm wishes from {{club_name}} and the Impact International community.",
  },
  Anniversary: {
    name: "Marriage anniversary greetings",
    subject: "Happy marriage anniversary, {{member_name}}!",
    body: "Dear {{member_name}}, wishing you and your spouse a very happy marriage anniversary! May your journey together be filled with love and happiness. Warm wishes from {{club_name}} and the Impact International community.",
  },
};

export default function GreetingSetup() {
  const { w } = useWorkspace();
  const a = useAction();
  const accounts = useGov(comm + "/accounts");
  const [form, setForm] = useState<any>();
  function start(trigger: keyof typeof presets) {
    setForm({ ...presets[trigger], trigger, orgId: w?.orgId, channel: "InApp", accountId: "", dailyLimit: 1000 });
  }
  const personalize = (text: string) => text
    .replaceAll("{{member_name}}", "Priya Sharma")
    .replaceAll("{{club_name}}", "Your club")
    .replaceAll("{{organization_name}}", "Your organization")
    .replaceAll("{{member_id}}", "MEM-001");
  return <section className="form-section">
    <h2>Automatic member greetings</h2>
    <p>Send a personal birthday or marriage anniversary message every year using the member’s saved dates.</p>
    <div className="gov-actions">
      <button onClick={() => start("Birthday")}>Add birthday greeting</button>
      <button className="secondary" onClick={() => start("Anniversary")}>Add marriage anniversary greeting</button>
    </div>
    <p className="muted">Approved rules check dates daily in UTC and send once per member per rule each day. Active members need a linked account and enabled occasion and promotional preferences. Birthday greetings also respect birthday announcements consent.</p>
    {form && <ModalForm title={form.trigger === "Birthday" ? "Birthday greeting" : "Marriage anniversary greeting"}
      onClose={() => setForm(undefined)} busy={a.busy} feedback={a.feedback}
      action="Save greeting draft" onSubmit={async (e: any) => {
        e.preventDefault();
        try {
          await a.run(comm + "/greetings", "POST", {
            rule: { orgId: form.orgId, name: form.name, trigger: form.trigger, templateId: "00000000-0000-0000-0000-000000000000", accountId: form.channel === "Email" ? form.accountId : null, dailyLimit: +form.dailyLimit },
            template: { orgId: form.orgId, name: form.name, channel: form.channel, subject: form.subject, body: form.body, category: form.trigger },
          });
          setForm(undefined);
        } catch { /* Action feedback keeps the entered message available for correction. */ }
      }}>
      <OrgSelect value={form.orgId} onChange={(orgId: string) => setForm({ ...form, orgId, accountId: "" })} />
      <Field label="Greeting name" name="name" required form={form} set={setForm} />
      <label>Send through<select value={form.channel} onChange={e => setForm({ ...form, channel: e.target.value, accountId: "" })}><option value="InApp">In-app notification</option><option value="Email">Email</option></select></label>
      {form.channel === "Email" && <label>Email account<select required value={form.accountId} onChange={e => setForm({ ...form, accountId: e.target.value })}>
        <option value="">Choose a verified email account</option>
        {accounts.data?.filter((v: any) => v.orgId === form.orgId && v.channel === "Email" && v.enabled && v.verified).map((v: any) => <option key={v.id} value={v.id}>{v.name}</option>)}
      </select></label>}
      <Field label="Subject / notification title" name="subject" required form={form} set={setForm} />
      <Field label="Greeting message" name="body" type="textarea" required form={form} set={setForm} />
      <p>Personalize with {"{{member_name}}, {{club_name}}, {{organization_name}}, {{member_id}}"}.</p>
      <Field label="Daily maximum (1–1000)" name="dailyLimit" type="number" form={form} set={setForm} />
      <article className="comm-message-preview"><strong>Example preview</strong><h3>{personalize(form.subject)}</h3><p>{personalize(form.body)}</p></article>
      <p>Save, then request approval in the rule list below. An independent approval activates automatic sending. Customize saved messages in the template library, then edit and resubmit the rule to approve the new version.</p>
    </ModalForm>}
    {a.feedback}
    <p><Link to="/app/communications/Templates">Customize saved message templates</Link></p>
  </section>;
}
