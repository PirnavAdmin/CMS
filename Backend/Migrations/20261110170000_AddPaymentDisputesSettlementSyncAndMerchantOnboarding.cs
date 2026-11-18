using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace AuthDemo.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261110170000_AddPaymentDisputesSettlementSyncAndMerchantOnboarding")]
public partial class AddPaymentDisputesSettlementSyncAndMerchantOnboarding : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(name:"KycStatus", table:"CmsOwnerMerchantSettings", type:"nvarchar(30)", maxLength:30, nullable:false, defaultValue:"NotStarted");
        m.AddColumn<string>(name:"ProviderMerchantAccountId", table:"CmsOwnerMerchantSettings", type:"nvarchar(150)", maxLength:150, nullable:true);
        m.AddColumn<string>(name:"KycNotes", table:"CmsOwnerMerchantSettings", type:"nvarchar(500)", maxLength:500, nullable:true);
        m.AddColumn<DateTime>(name:"KycSubmittedAt", table:"CmsOwnerMerchantSettings", type:"datetime2", nullable:true);
        m.AddColumn<DateTime>(name:"KycReviewedAt", table:"CmsOwnerMerchantSettings", type:"datetime2", nullable:true);
        m.AddColumn<string>(name:"KycStatus", table:"HospitalMerchantSettings", type:"nvarchar(30)", maxLength:30, nullable:false, defaultValue:"NotStarted");
        m.AddColumn<string>(name:"ProviderMerchantAccountId", table:"HospitalMerchantSettings", type:"nvarchar(150)", maxLength:150, nullable:true);
        m.AddColumn<string>(name:"KycNotes", table:"HospitalMerchantSettings", type:"nvarchar(500)", maxLength:500, nullable:true);
        m.AddColumn<DateTime>(name:"KycSubmittedAt", table:"HospitalMerchantSettings", type:"datetime2", nullable:true);
        m.AddColumn<DateTime>(name:"KycReviewedAt", table:"HospitalMerchantSettings", type:"datetime2", nullable:true);

        m.CreateTable("HospitalPaymentDisputes", table => new
        {
            Id=table.Column<long>(type:"bigint",nullable:false).Annotation("SqlServer:Identity","1, 1"),
            HospitalId=table.Column<int>(type:"int",nullable:false), HospitalPaymentTransactionId=table.Column<long>(type:"bigint",nullable:false),
            GatewayDisputeId=table.Column<string>(type:"nvarchar(150)",maxLength:150,nullable:false), GatewayPaymentId=table.Column<string>(type:"nvarchar(150)",maxLength:150,nullable:false),
            Amount=table.Column<decimal>(type:"decimal(18,2)",nullable:false), Currency=table.Column<string>(type:"nvarchar(10)",maxLength:10,nullable:false,defaultValue:"INR"), Reason=table.Column<string>(type:"nvarchar(300)",maxLength:300,nullable:true),
            Status=table.Column<string>(type:"nvarchar(40)",maxLength:40,nullable:false), EvidenceDueBy=table.Column<DateTime>(type:"datetime2",nullable:true), PayloadJson=table.Column<string>(type:"nvarchar(max)",nullable:false), EvidenceDocumentIdsJson=table.Column<string>(type:"nvarchar(max)",nullable:false,defaultValue:"[]"),
            CreatedAt=table.Column<DateTime>(type:"datetime2",nullable:false), UpdatedAt=table.Column<DateTime>(type:"datetime2",nullable:false), ClosedAt=table.Column<DateTime>(type:"datetime2",nullable:true)
        }, constraints: table => { table.PrimaryKey("PK_HospitalPaymentDisputes",x=>x.Id); table.ForeignKey("FK_HospitalPaymentDisputes_HospitalPaymentTransactions_HospitalPaymentTransactionId",x=>x.HospitalPaymentTransactionId,"HospitalPaymentTransactions","Id",onDelete:ReferentialAction.Restrict); table.ForeignKey("FK_HospitalPaymentDisputes_Hospitals_HospitalId",x=>x.HospitalId,"Hospitals","Id",onDelete:ReferentialAction.Restrict); });
        m.CreateIndex("IX_HospitalPaymentDisputes_HospitalId_GatewayDisputeId","HospitalPaymentDisputes",new[]{"HospitalId","GatewayDisputeId"},unique:true);
        m.CreateIndex("IX_HospitalPaymentDisputes_HospitalId_Status","HospitalPaymentDisputes",new[]{"HospitalId","Status"});

        m.CreateTable("CmsSubscriptionDisputes", table => new
        {
            Id=table.Column<long>(type:"bigint",nullable:false).Annotation("SqlServer:Identity","1, 1"), SubscriptionPurchaseId=table.Column<int>(type:"int",nullable:false),
            GatewayDisputeId=table.Column<string>(type:"nvarchar(150)",maxLength:150,nullable:false), GatewayPaymentId=table.Column<string>(type:"nvarchar(150)",maxLength:150,nullable:false), Mode=table.Column<string>(type:"nvarchar(20)",maxLength:20,nullable:false),
            Amount=table.Column<decimal>(type:"decimal(18,2)",nullable:false), Currency=table.Column<string>(type:"nvarchar(10)",maxLength:10,nullable:false,defaultValue:"INR"), Reason=table.Column<string>(type:"nvarchar(300)",maxLength:300,nullable:true), Status=table.Column<string>(type:"nvarchar(40)",maxLength:40,nullable:false),
            EvidenceDueBy=table.Column<DateTime>(type:"datetime2",nullable:true), PayloadJson=table.Column<string>(type:"nvarchar(max)",nullable:false), EvidenceDocumentIdsJson=table.Column<string>(type:"nvarchar(max)",nullable:false,defaultValue:"[]"), CreatedAt=table.Column<DateTime>(type:"datetime2",nullable:false), UpdatedAt=table.Column<DateTime>(type:"datetime2",nullable:false), ClosedAt=table.Column<DateTime>(type:"datetime2",nullable:true)
        }, constraints: table => { table.PrimaryKey("PK_CmsSubscriptionDisputes",x=>x.Id); table.ForeignKey("FK_CmsSubscriptionDisputes_SubscriptionPurchases_SubscriptionPurchaseId",x=>x.SubscriptionPurchaseId,"SubscriptionPurchases","Id",onDelete:ReferentialAction.Restrict); });
        m.CreateIndex("IX_CmsSubscriptionDisputes_GatewayDisputeId","CmsSubscriptionDisputes","GatewayDisputeId",unique:true);
        m.CreateIndex("IX_CmsSubscriptionDisputes_SubscriptionPurchaseId_Mode","CmsSubscriptionDisputes",new[]{"SubscriptionPurchaseId","Mode"});

        m.CreateTable("HospitalGatewaySettlements", table => new
        {
            Id=table.Column<long>(type:"bigint",nullable:false).Annotation("SqlServer:Identity","1, 1"), HospitalId=table.Column<int>(type:"int",nullable:false), Mode=table.Column<string>(type:"nvarchar(20)",maxLength:20,nullable:false), SettlementId=table.Column<string>(type:"nvarchar(150)",maxLength:150,nullable:false), Status=table.Column<string>(type:"nvarchar(30)",maxLength:30,nullable:false), Amount=table.Column<decimal>(type:"decimal(18,2)",nullable:false), FeeAmount=table.Column<decimal>(type:"decimal(18,2)",nullable:false), TaxAmount=table.Column<decimal>(type:"decimal(18,2)",nullable:false), Utr=table.Column<string>(type:"nvarchar(150)",maxLength:150,nullable:true), SettledAt=table.Column<DateTime>(type:"datetime2",nullable:false), SyncedAt=table.Column<DateTime>(type:"datetime2",nullable:false)
        }, constraints: table => { table.PrimaryKey("PK_HospitalGatewaySettlements",x=>x.Id); table.ForeignKey("FK_HospitalGatewaySettlements_Hospitals_HospitalId",x=>x.HospitalId,"Hospitals","Id",onDelete:ReferentialAction.Restrict); });
        m.CreateIndex("IX_HospitalGatewaySettlements_HospitalId_Mode_SettlementId","HospitalGatewaySettlements",new[]{"HospitalId","Mode","SettlementId"},unique:true);

        m.CreateTable("CmsGatewaySettlements", table => new
        {
            Id=table.Column<long>(type:"bigint",nullable:false).Annotation("SqlServer:Identity","1, 1"), Mode=table.Column<string>(type:"nvarchar(20)",maxLength:20,nullable:false), SettlementId=table.Column<string>(type:"nvarchar(150)",maxLength:150,nullable:false), Status=table.Column<string>(type:"nvarchar(30)",maxLength:30,nullable:false), Amount=table.Column<decimal>(type:"decimal(18,2)",nullable:false), FeeAmount=table.Column<decimal>(type:"decimal(18,2)",nullable:false), TaxAmount=table.Column<decimal>(type:"decimal(18,2)",nullable:false), Utr=table.Column<string>(type:"nvarchar(150)",maxLength:150,nullable:true), SettledAt=table.Column<DateTime>(type:"datetime2",nullable:false), SyncedAt=table.Column<DateTime>(type:"datetime2",nullable:false)
        }, constraints: table => table.PrimaryKey("PK_CmsGatewaySettlements",x=>x.Id));
        m.CreateIndex("IX_CmsGatewaySettlements_Mode_SettlementId","CmsGatewaySettlements",new[]{"Mode","SettlementId"},unique:true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("HospitalPaymentDisputes"); m.DropTable("CmsSubscriptionDisputes"); m.DropTable("HospitalGatewaySettlements"); m.DropTable("CmsGatewaySettlements");
        m.DropColumn("KycStatus","CmsOwnerMerchantSettings"); m.DropColumn("ProviderMerchantAccountId","CmsOwnerMerchantSettings"); m.DropColumn("KycNotes","CmsOwnerMerchantSettings"); m.DropColumn("KycSubmittedAt","CmsOwnerMerchantSettings"); m.DropColumn("KycReviewedAt","CmsOwnerMerchantSettings");
        m.DropColumn("KycStatus","HospitalMerchantSettings"); m.DropColumn("ProviderMerchantAccountId","HospitalMerchantSettings"); m.DropColumn("KycNotes","HospitalMerchantSettings"); m.DropColumn("KycSubmittedAt","HospitalMerchantSettings"); m.DropColumn("KycReviewedAt","HospitalMerchantSettings");
    }
}
