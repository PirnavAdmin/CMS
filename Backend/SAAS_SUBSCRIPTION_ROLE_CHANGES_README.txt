CMS SaaS / ROLE OWNERSHIP CHANGES
=================================
1. SuperAdmin dashboard no longer exposes clinic billing/revenue/performance/recent user activity.
2. SuperAdmin creates Admin without requiring HospitalId.
3. Admin first login can have HospitalId=null. Admin creates clinic via POST /api/clinics; backend assigns that clinic to the logged-in Admin. Login again afterward to receive HospitalId in JWT.
4. Clinic CRUD is Admin-only and scoped to own clinic.
5. Branch CRUD is Admin-only and ignores request HospitalId; JWT HospitalId is authoritative.
6. Admin listing includes clinic plus branchCount and branchNames for SuperAdmin display.
7. SuperAdminReports contains SaaS subscription summary only. Clinic revenue reports are Admin-only/scoped.
8. AuditLogs: SuperAdmin sees only Role=SuperAdmin logs. Admin sees own clinic users, excluding SuperAdmin.
9. SubscriptionPlan supports monthly/6-month/yearly (or any DurationMonths), price, IncludesLab, active status.
10. ClinicSubscription assigns one plan to the entire clinic and all its branches.
11. /api/Lab is backend-blocked with HTTP 403 when active subscription does not include Lab.
12. Prescription supports ExternalTestNames. Without Lab, test names are saved as ExternalRecommended and no LabOrder is created.
13. Migration 20261007120000_AddSaasSubscriptions creates SubscriptionPlans and ClinicSubscriptions.

IMPORTANT LOCAL VERIFICATION
----------------------------
dotnet restore
dotnet build
dotnet ef database update

This execution environment did not contain the dotnet SDK, so compilation could not be executed here.
