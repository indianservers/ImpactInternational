import { useEffect } from "react";
import { Link, useLocation } from "react-router-dom";
export const impact = {
  name: "Impact International", network: "Impact International", color: "#302b78",
  logo: "/brand/impact-logo.webp", website: "https://impactinternational.in/",
  pillars: ["Training", "Business", "Service", "Leadership"],
};
const pages: [string, string, string][] = [
  ["/app/international", "International headquarters", "Impact International oversees National, Regional, District, Zone, and Club levels."],
  ["/app/national", "Manage national units", "Manage the National level reporting to Impact International headquarters."],
  ["/app/regions", "Manage regions", "Create and manage regions within their national unit."],
  ["/app/districts", "Manage districts", "Create and manage districts within their regions."],
  ["/app/zones", "Manage zones", "Create and manage zones within their districts."],
  ["/app/clubs/directory", "Impact club directory", "Explore club leadership, membership, and activity."],
  ["/app/events/new", "Create an Impact event", "Plan an Impact International training session, business gathering, service activity, or leadership program."],
  ["/app/events/:id/checkin", "Impact event check-in", "Welcome participants and record attendance at your Impact International event."],
  ["/app/events/:id", "Impact event workspace", "Manage the program, participants, and records for this Impact International gathering."],
  ["/app/members/:id", "Impact member profile", "Manage this Impact International member’s details, affiliations, and history."],
  ["/app/clubs/:id", "Impact club workspace", "Your club’s members, leaders, and activities within Impact International."],
  ["/app/committees/:id", "Impact committee workspace", "Coordinate responsibilities and activity for this Impact International committee."],
  ["/app/operations/meetings", "Impact meetings", "Prepare agendas, record minutes, and follow through on Impact International decisions."],
  ["/app/operations/tasks", "Impact tasks", "Assign responsibilities and track follow-ups for Impact International teams."],
  ["/app/operations/projects", "Impact service projects", "Plan community service, record progress, and track the outcomes of Impact International projects."],
  ["/app/operations/volunteers", "Impact volunteers", "Connect Impact International members with service opportunities, shifts, and volunteer hours."],
  ["/app/operations/impact", "Impact service outcomes", "Understand the contributions and verified outcomes of Impact International service."],
  ["/app/operations/documents", "Impact document library", "Keep Impact International operational documents organized and available to authorized teams."],
  ["/app/operations/certificates", "Impact certificates", "Recognize participation and service with Impact International certificates."],
  ["/app/finance/member", "My Impact dues & payments", "View your Impact International dues, invoices, payments, and receipts."],
  ["/app/finance/invoices", "Impact invoices", "Track invoices and amounts due for Impact International memberships and activities."],
  ["/app/finance/dues", "Impact membership dues", "Set up dues and renewal charges for Impact International clubs and organizational units."],
  ["/app/finance/payments", "Impact payments & receipts", "Record payments and manage receipts for Impact International activities."],
  ["/app/finance/reconciliation", "Impact bank reconciliation", "Match bank transactions with Impact International financial records."],
  ["/app/finance/accounts", "Impact accounts & journals", "Maintain the chart of accounts and accountable journal records for Impact International."],
  ["/app/finance/budgets", "Impact budgets", "Plan and track budgets for Impact International teams, events, and projects."],
  ["/app/finance/expenses", "Impact expenses", "Review expenses and reimbursements supporting Impact International activities."],
  ["/app/finance/funding", "Support Impact International", "Manage donation and sponsorship records for Impact International service and programs."],
  ["/app/finance/sources", "Impact activity finance", "Connect Impact International event and project activity to its financial records."],
  ["/app/finance/allocations", "Impact fund allocations", "Track authorized allocations between Impact International financial entities."],
  ["/app/finance/reports", "Impact financial reports", "Review financial performance and statements for your Impact International scope."],
  ["/app/finance/settings", "Impact finance settings", "Configure financial entities, periods, and controls for Impact International."],
  ["/app/intelligence/assistant", "Ask Impact intelligence", "Ask questions about the Impact International records you are authorized to access."],
  ["/app/intelligence/knowledge", "Impact knowledge library", "Find sourced answers in Impact International documents and policies."],
  ["/app/intelligence/workflows", "Impact follow-up workflows", "Coordinate timely member and club follow-ups with accountable human review."],
  ["/app/intelligence/metrics", "Impact metrics catalog", "Explore definitions behind Impact International membership, participation, and service metrics."],
  ["/app/intelligence/branding", "Impact portal experience", "Manage Impact International terminology, portal features, and website configuration."],
  ["/app/intelligence/operations", "Impact system diagnostics", "Check the operating status of the Impact International portal."],
  ["/app/id-cards/templates", "Impact card templates", "Create official Impact International membership card designs."],
  ["/app/id-cards/designer/:id", "Impact card designer", "Design a clear, consistent Impact International member identity."],
  ["/app/id-cards/issuances", "Impact card issuance", "Issue and manage membership cards for eligible Impact International members."],
  ["/app/id-cards/bulk", "Impact bulk card issuance", "Prepare membership cards for authorized groups of Impact International members."],
  ["/app/id-cards/print", "Impact card printing", "Prepare official Impact International membership cards for printing."],
  ["/app/id-cards/settings", "Impact card policies", "Manage card validity and issuance controls for Impact International."],
  ["/join", "Join Impact International", "Accept your invitation to the Impact International community."],
  ["/event/:id", "Impact International event", "Discover and register for this Impact International gathering."],
  ["/event-invitation", "Impact event invitation", "Respond to your invitation to an Impact International event."],
  ["/resend-verification", "Verify your Impact account", "Request verification instructions for your Impact International account."],

  ["/app/foundation-overview", "Impact overview", "Membership, leadership, and service across Impact International."],
  ["/app/club-administration", "Club administration", "Support healthy Impact clubs and responsible leadership."],
  ["/app/membership-insights", "Membership insights", "Understand growth and participation across Impact International."],
  ["/app/membership-settings", "Membership settings", "Define the membership journey for Impact International clubs."],
  ["/app/governance-settings", "Governance settings", "Configure Impact offices, approvals, and accountability."],
  ["/app/governance-documents", "Impact documents", "Keep policies, constitutions, and official records together."],
  ["/app/leadership-calendar", "Leadership calendar", "Plan terms and continuity across Impact leadership."],
  ["/app/event-intelligence", "Event reports", "Understand participation in Impact gatherings and programs."],
  ["/app/event-settings", "Event settings", "Set up training, business, service, and leadership gatherings."],
  ["/app/organizations", "Impact structure", "International → National → Regions → Districts → Zones → Clubs."],
  ["/app/administration", "Roles and access", "Give Impact teams the access their responsibilities require."],
  ["/app/approvals", "Membership approvals", "Welcome new members through the Impact admission process."],
  ["/app/transfers", "Transfers and requests", "Support members as they move between Impact clubs."],
  ["/app/imports", "Bulk import", "Import members and dues with validation, duplicate checks and audit history."],
  ["/app/members", "Impact members", "Manage member profiles, affiliations, and membership records."],
  ["/app/directory", "Impact member directory", "Connect with people and expertise across Impact International."],
  ["/app/clubs", "Impact clubs", "Build local communities for learning, enterprise, and service."],
  ["/app/leadership", "Impact leadership", "Recognize the people entrusted with leading Impact communities."],
  ["/app/handovers", "Leadership handovers", "Pass knowledge and responsibilities to the next Impact leaders."],
  ["/app/committees", "Impact committees", "Bring people together around clear responsibilities and goals."],
  ["/app/governance", "Impact governance", "Review proposals and preserve accountable decisions."],
  ["/app/resolutions", "Impact resolutions", "Record decisions that guide the Impact community."],
  ["/app/my-membership", "My Impact membership", "Your memberships, club connections, and personal progress."],
  ["/app/my-id", "My Impact ID", "Your digital identity in the Impact International community."],
  ["/app/id-cards", "Impact ID Card Studio", "Design and manage official Impact membership cards."],
  ["/app/my-events", "My Impact events", "Your invitations, registrations, and event passes."],
  ["/app/events", "Impact events", "Gather for training, business, service, and leadership."],
  ["/app/operations", "Impact operations", "Turn meetings, projects, and volunteer efforts into action."],
  ["/app/communications", "Impact communications", "Keep members and clubs connected with timely messages."],
  ["/app/finance", "Impact finance", "Manage dues, payments, and accountable financial records."],
  ["/app/intelligence", "Impact intelligence", "Explore Impact records and turn information into informed action."],
  ["/app/settings", "Impact settings", "Manage the shared preferences of Impact International."],
  ["/app/audit", "Impact audit trail", "Trace changes and decisions across your authorized scope."],
  ["/app/profile", "My Impact account", "Manage your details, preferences, and active sessions."],
  ["/app/apply", "Join an Impact club", "Begin your membership journey with Impact International."],
  ["/app", "Impact International home", "Training. Business. Service. Leadership."],
  ["/login", "Sign in", ""], ["/signup", "Join Impact International", ""],
  ["/forgot-password", "Reset password", ""], ["/complete", "Account verification", ""],
  ["/verify", "Verify an Impact record", ""], ["/invitation", "Impact invitation", ""],
];
export function pageIdentity(path: string) {
  const parts = path.split("/").filter(Boolean);
  const match = pages.filter(([prefix]) => {
    const pattern = prefix.split("/").filter(Boolean);
    return pattern.length <= parts.length && pattern.every((segment, index) => segment.startsWith(":") || segment === parts[index]);
  }).sort((a, b) => b[0].split("/").length - a[0].split("/").length || Number(a[0].includes(":")) - Number(b[0].includes(":")))[0];
  return match || ["/", "Welcome to Impact International", "Training. Business. Service. Leadership."];
}
export function ImpactDocumentTitle() {
  const { pathname } = useLocation();
  useEffect(() => {
    document.title = pageIdentity(pathname)[1] + " | " + impact.name;
    const icon = document.querySelector<HTMLLinkElement>('link[rel~="icon"]');
    if (icon) { icon.href = impact.logo; icon.type = "image/webp"; }
  }, [pathname]);
  return null;
}
export function ImpactPageContext() {
  const { pathname } = useLocation();
  const [, title, description] = pageIdentity(pathname);
  return <div className="foundation-page-context"><div><span>IMPACT INTERNATIONAL / {title}</span><p>{description}</p></div><Link to="/about" aria-label="About Impact International"><img src={impact.logo} width="42" height="42" alt="" /></Link></div>;
}
export function impactRole(name?: string) {
  return name === "Association Super Admin" ? "Impact Administrator" : name || "Impact member";
}
