ALL REQUESTED CHANGES - 2026-07-31

Implemented:
1. Nurse registration/login through /api/Auth/register-nurse and /api/Auth/login.
2. Nurse receives credentials email and MustChangePassword=true.
3. Doctor and Receptionist credential email flow standardized.
4. Admin-assigned permissions remain stored in UserPermissions.
5. Doctor can be assigned multiple branches through PUT /api/Doctor/{doctorId}/branches.
6. Doctor prescription submit now accepts Medicines and LabTests in one request.
7. Nurse print queue returns medicines and lab tests.
8. Nurse can mark a prescription printed using PATCH /api/Nurse/prescriptions/{id}/printed.
9. Receptionist billing supports OP, Lab and Pharmacy with GST.
10. GST report added: GET /api/Report/gst-summary.
11. Admin and Doctor schedule update/delete endpoints remain available.
12. Logout audit remains available through POST /api/Auth/logout.

Database:
Run Update-Database after the solution builds. New migration:
20260731103000_AddPrescriptionLabTestsAndPrintTracking

Important:
Configure EmailSettings in appsettings.json with a valid SMTP account/App Password.
The current execution environment did not contain the .NET SDK, so dotnet build could not be run here.
