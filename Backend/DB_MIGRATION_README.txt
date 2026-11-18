CMS source-only project - database reconciliation

This archive contains source code only; it deliberately excludes downloaded/compiled
DLLs and executables. It has NOT been compiled or run against your SQL Server.

1. Back up CMSDB before schema changes.
2. Extract the ZIP to a NEW folder outside Downloads if possible.
3. Open Adminclinic.csproj in Visual Studio and restore NuGet packages.
4. Review connection string and the pending migration:
   Migrations/20261012150000_AddSubscriptionPurchases.cs
5. This migration now checks for an existing SubscriptionPurchases table and
   adds PurchasedDurationMonths and PurchasedIncludesLab only when missing.
   It also creates missing indexes. It is non-destructive on rollback.
6. If the original table exists from an earlier migration, do not delete it.
7. Inspect SQL output using Script-Migration or dotnet ef migrations script
   before running Update-Database on CMSDB.
8. Existing rows will have PurchasedDurationMonths=0 and PurchasedIncludesLab=0.
   These are NOT trustworthy purchase snapshots. Recover actual values from
   original purchase records before activation/renewal; do not guess.
9. Existing Windows Security 'blocked .NET host' issue is machine-specific;
   do not bypass protection or whitelist an unknown DLL. Source-only rebuild
   avoids shipping precompiled project binaries but cannot guarantee that the
   machine's application control policy will allow a newly compiled assembly.
10. An EF warning about BillingPaymentTransaction.BillingId1 is not resolved
    by this migration; relationship configuration needs separate review.

This package is not certified error-free. Do not deploy production payments
or OTP/password-reset workflows without security review.
