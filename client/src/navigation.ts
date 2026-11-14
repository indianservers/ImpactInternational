import { LayoutDashboard, Network, Users, ShieldCheck, ClipboardCheck, CalendarDays, Settings, CreditCard, Wallet, Briefcase, Mail, ChartNoAxesCombined, UserRound } from "lucide-react";

type Tab = { label: string; to: string; permissions?: string[] };
export const financeMenu = [
  ["", "Finance Overview"],
  ["transactions", "Transactions"],
  ["receipts", "Receipts & Collections"],
  ["invoices", "Invoices & Billing"],
  ["payments", "Payments"],
  ["expenses", "Expenses"],
  ["reconciliation", "Bank Reconciliation"],
  ["dues", "Dues & Membership"],
  ["budgets", "Budgets & Allocation"],
  ["reports", "Financial Reports & Analytics"],
  ["audit", "Audit Trail"],
] as const;
const tab = (label: string, path: string, ...permissions: string[]): Tab => ({ label, to: `/app${path}`, permissions });

// Keep feature URLs stable: bookmarks, detail links and browser history select the same tab.
const featureSections = [
  { id: "home", label: "Dashboard", icon: LayoutDashboard, tabs: [tab("Overview", "", "organization.view"), tab("Impact overview", "/foundation-overview", "organization.view")] },
  { id: "membership", label: "Membership", icon: Users, tabs: [
    tab("Members", "/members", "members.view"), tab("Directory", "/directory", "directory.view"),
    tab("Applications", "/approvals", "members.approve"), tab("Transfers & requests", "/transfers", "members.transfer.approve"),
    tab("Import", "/imports", "members.import"), tab("Insights", "/membership-insights", "members.view"),
    tab("Settings", "/membership-settings", "members.workflow.manage"),
  ] },
  { id: "organizations", label: "Impact structure", icon: Network, tabs: [
    tab("Structure overview", "/organizations", "organization.view"), tab("International", "/international", "organization.view"), tab("National", "/national", "organization.view"), tab("Regions", "/regions", "organization.view"), tab("Districts", "/districts", "organization.view"), tab("Zones", "/zones", "organization.view"), tab("Clubs", "/clubs", "organization.view"),
    tab("Club overview", "/club-administration", "governance.reports"),
  ] },
  { id: "leadership", label: "Leadership", icon: ShieldCheck, tabs: [
    tab("Leadership Overview", "/leadership", "leadership.view"), tab("People Directory", "/leadership/directory", "leadership.view"), tab("Leadership Intelligence", "/leadership/intelligence", "leadership.view"), tab("Office Bearers", "/leadership/office-bearers", "leadership.view"), tab("Appointments", "/leadership/appointments", "leadership.view"), tab("Vacancies", "/leadership/vacancies", "leadership.view"), tab("Succession", "/leadership/succession", "leadership.view"), tab("History", "/leadership/history", "leadership.view"), tab("Office Positions", "/leadership/positions", "governance.configure"), tab("Calendar", "/leadership-calendar", "leadership.view"),
    tab("Handovers", "/handovers", "leadership.view"), tab("Committees", "/committees", "committees.view"),
  ] },
  { id: "governance", label: "Governance", icon: ClipboardCheck, tabs: [
    tab("Approvals", "/governance", "clubs.view", "clubs.manage", "leadership.view", "leadership.manage", "governance.configure", "governance.approve", "governance.restructure", "governance.documents", "committees.view", "committees.manage", "governance.reports"),
    tab("Resolutions", "/resolutions", "governance.documents"), tab("Documents", "/governance-documents", "governance.documents"),
    tab("Configuration", "/governance-settings", "governance.configure"),
  ] },
  { id: "events", label: "Events", icon: CalendarDays, tabs: [
    tab("Calendar & events", "/events", "events.view"), tab("My invitations", "/my-events"),
    tab("Reports", "/event-intelligence", "events.reports"), tab("Event types", "/event-settings", "events.configure"),
  ] },
  { id: "operations", label: "Operations", icon: Briefcase, tabs: [tab("Operations", "/operations", "operations.view")] },
  { id: "communications", label: "Communications", icon: Mail, tabs: [tab("Communications", "/communications", "communications.view")] },
  { id: "finance", label: "Finance", icon: Wallet, tabs: [tab("Finance", "/finance", "finance.self")] },
  { id: "cards", label: "ID Card Studio", icon: CreditCard, tabs: [tab("ID Card Studio", "/id-cards", "members.cards"), tab("My digital ID", "/my-id")] },
  { id: "intelligence", label: "Intelligence", icon: ChartNoAxesCombined, tabs: [tab("Intelligence", "/intelligence", "intelligence.assistant")] },
  { id: "administration", label: "Administration", icon: Settings, tabs: [
    tab("Roles & access", "/administration", "roles.view"), tab("Audit trail", "/audit", "audit.view"), tab("Impact settings", "/settings", "settings.view"),
  ] },
  { id: "community", label: "My Impact community", icon: UserRound, tabs: [
    tab("My membership", "/my-membership"), tab("Join a club", "/apply"), tab("Profile & security", "/profile"),
  ] },
];

// A feature belongs to one area and one task tab. Only the selected task's tools appear.
const areaDefinitions = [
  { id: "home", label: "Home", icon: LayoutDashboard, groups: [["Overview", "home"]] },
  { id: "people", label: "People", icon: Users, groups: [["Members", "membership"], ["Applications & transfers", "requests"], ["My membership", "community"]] },
  { id: "leadership", label: "Leadership", icon: ShieldCheck, groups: [["Leadership & People", "leadership"]] },
  { id: "organization", label: "Organization", icon: Network, groups: [["Structure & clubs", "organizations"], ["Decisions & records", "governance"]] },
  { id: "activities", label: "Activities", icon: CalendarDays, groups: [["Events", "events"], ["Meetings & projects", "operations"], ["Messages", "communications"]] },
  { id: "cards", label: "ID cards", icon: CreditCard, groups: [["ID cards", "cards"]] },
  { id: "tools", label: "Finance", icon: Wallet, groups: [["Finance", "finance"]] },
  { id: "intelligence", label: "Intelligence", icon: ChartNoAxesCombined, groups: [["Intelligence", "intelligence"]] },
  { id: "administration", label: "Settings & access", icon: Settings, groups: [["Access & audit", "administration"], ["Module settings", "configuration"]] },
];
const configurationPaths = ["/app/membership-settings", "/app/governance-settings", "/app/event-settings"];
const features = featureSections.flatMap(section => section.tabs.map(t => ({ ...t, groupId: configurationPaths.includes(t.to) ? "configuration" : ["/app/approvals", "/app/transfers"].includes(t.to) ? "requests" : section.id })));
export const navigationSections = areaDefinitions.map(area => ({
  ...area,
  tabs: area.groups.flatMap(([groupLabel, groupId]) => features.filter(t => t.groupId === groupId).map(t => ({
    ...t, groupLabel,
    label: t.to === "/app/members" ? "All members" : t.to === "/app/membership-settings" ? "Membership" : t.to === "/app/governance-settings" ? "Governance" : t.label,
  }))),
}));
export function taskGroups(tabs: typeof navigationSections[number]["tabs"]) {
  return [...new Set(tabs.map(t => t.groupId))].map(id => ({ id, label: tabs.find(t => t.groupId === id)!.groupLabel, tabs: tabs.filter(t => t.groupId === id) }));
}

const featurePermissions: Record<string, string[]> = {
  assistant: ["intelligence.assistant"], knowledge: ["intelligence.knowledge"],
  workflows: ["intelligence.automations"], events: ["events."],
  operations: ["operations."], finance: ["finance."],
};
export function canOpenTab(tab: Tab, permissions: string[], disabledFeatures: string[]) {
  if (tab.to === "/app/my-events" && disabledFeatures.includes("events")) return false;
  return !disabledFeatures.some((feature) =>
    (featurePermissions[feature] || []).some((prefix) => tab.permissions?.some((p) => p.startsWith(prefix))),
  ) && (!tab.permissions?.length || tab.permissions.some((p) => permissions.includes(p)));
}
export function matchesTab(pathname: string, target: string) {
  return pathname === target || (target !== "/app" && pathname.startsWith(`${target}/`));
}
