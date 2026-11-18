LAB MANAGEMENT ADDED

Roles: Admin and LabTechnician. Create LabTechnician using existing POST /api/Staff with Role=LabTechnician and BranchId.

Excel dump included at: wwwroot/downloads/Lab_Test_Master_Dump.xlsx
Static URL: /downloads/Lab_Test_Master_Dump.xlsx

Main APIs:
GET /api/Lab/master
GET /api/Lab/master/export
POST /api/Lab/master/import
POST /api/Lab/master
PUT /api/Lab/master/{id}
DELETE /api/Lab/master/{id}
GET /api/Lab/orders
GET /api/Lab/orders/{id}
PATCH /api/Lab/orders/{id}/sample-collected
PATCH /api/Lab/orders/{id}/start
PUT /api/Lab/orders/{id}/result
POST /api/Lab/orders/{id}/report
PATCH /api/Lab/orders/{id}/complete
GET /api/Lab/doctor/reports
GET /api/Lab/patient/reports
GET /api/Lab/orders/{id}/report/download

Database: add a migration after reviewing your current snapshot: Add-Migration AddLabManagement; Update-Database.

Dashboard: GET /api/Lab/dashboard (Admin, LabTechnician).
Lab technician CRUD: use POST/PUT/DELETE /api/Staff and GET /api/Staff/lab-technicians with Role=LabTechnician.
