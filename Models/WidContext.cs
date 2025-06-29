using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace EkycInquiry.Models;

public partial class WidContext : DbContext
{
    public WidContext()
    {
    }

    public WidContext(DbContextOptions<WidContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdditionalDatum> AdditionalData { get; set; }

    public virtual DbSet<Audit> Audits { get; set; }

    public virtual DbSet<ContactRequest> ContactRequests { get; set; }
    public DbSet<LineNoNavigation> LinesA { get; set; }

    public virtual DbSet<Line> Lines { get; set; }

    public virtual DbSet<Link> Links { get; set; }

    public virtual DbSet<Medium> Media { get; set; }

    public virtual DbSet<Mrzblacklist> Mrzblacklists { get; set; }

    public virtual DbSet<Package> Packages { get; set; }

    public virtual DbSet<PrismaMigration> PrismaMigrations { get; set; }

    public virtual DbSet<PrismaMigration1> PrismaMigrations1 { get; set; }

    public virtual DbSet<Recharge> Recharges { get; set; }

    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<SessionComponent> SessionComponents { get; set; }

    public virtual DbSet<SessionSection> SessionSections { get; set; }

    public virtual DbSet<SessionStep> SessionSteps { get; set; }

    public virtual DbSet<SessionWorkflow> SessionWorkflows { get; set; }

    public virtual DbSet<Token> Tokens { get; set; }

    public virtual DbSet<TokenDocumentDatum> TokenDocumentData { get; set; }

    public virtual DbSet<TokenUserAddressDatum> TokenUserAddressData { get; set; }

    public virtual DbSet<TokenUserDatum> TokenUserData { get; set; }

    public virtual DbSet<TokenValidationDatum> TokenValidationData { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAddress> UserAddresses { get; set; }

    public virtual DbSet<UserDocumentDatum> UserDocumentData { get; set; }

    public virtual DbSet<Validation> Validations { get; set; }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //{
    //    optionsBuilder.LogTo(Console.WriteLine);
    //    var dbBuilder = new NpgsqlDataSourceBuilder("Host=192.168.246.11;Database=wid;Username=postgres;Password=EKYCdb@123");
    //    dbBuilder.EnableDynamicJson();
    //    optionsBuilder.UseNpgsql(dbBuilder.Build());
    //}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("zain", "DataCheck", new[] { "mandatory", "not_mandatory" })
            .HasPostgresEnum("zain", "DocumentType", new[] { "driver_license", "identity_card", "passport", "generic" })
            .HasPostgresEnum("zain", "Gender", new[] { "female", "male" })
            .HasPostgresEnum("zain", "KycType", new[] { "videorecord", "custom", "custom1", "custom2", "custom3", "fea" })
            .HasPostgresEnum("zain", "Locale", new[] { "en", "it", "ar" })
            .HasPostgresEnum("zain", "SessionStatus", new[] { "approval_pending", "approved", "created", "done", "rejected_by_backoffice", "rejected_by_batch", "signature_pending", "to_be_deleted", "working", "to_discard", "archived", "ready_to_archive", "sent_to_archive" })
            .HasPostgresEnum("zain", "TokenStatus", new[] { "done", "created", "approval_pending", "working" })
            .HasPostgresEnum("zain", "UserRoles", new[] { "consumer", "operator", "user", "system" })
            .HasPostgresEnum("zain", "WorkflowType", new[] { "operator", "user" });

        modelBuilder.Entity<AdditionalDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AdditionalData_pkey");

            entity.ToTable("AdditionalData", "zain");

            entity.HasIndex(e => e.TokenId, "AdditionalData_tokenId_key").IsUnique();

            entity.HasIndex(e => e.Uid, "AdditionalData_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Content)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("content");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.TokenId).HasColumnName("tokenId");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.Token).WithOne(p => p.AdditionalDatum)
                .HasForeignKey<AdditionalDatum>(d => d.TokenId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("AdditionalData_tokenId_fkey");
        });

        modelBuilder.Entity<Audit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Audit_pkey");

            entity.ToTable("Audit", "zain");

            entity.HasIndex(e => e.Uid, "Audit_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Content)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("content");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.SessionId).HasColumnName("sessionId");
            entity.Property(e => e.TokenId).HasColumnName("tokenId");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.Session).WithMany(p => p.Audits)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("Audit_sessionId_fkey");

            entity.HasOne(d => d.Token).WithMany(p => p.Audits)
                .HasForeignKey(d => d.TokenId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("Audit_tokenId_fkey");
        });

        modelBuilder.Entity<ContactRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ContactRequest_pkey");

            entity.ToTable("ContactRequest", "zain");

            entity.HasIndex(e => e.Uid, "ContactRequest_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Firstname).HasColumnName("firstname");
            entity.Property(e => e.Lastname).HasColumnName("lastname");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.PhoneNumber).HasColumnName("phoneNumber");
            entity.Property(e => e.PhonePrefix).HasColumnName("phonePrefix");
            entity.Property(e => e.SessionId).HasColumnName("sessionId");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.Session).WithMany(p => p.ContactRequests)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ContactRequest_sessionId_fkey");
        });

        modelBuilder.Entity<Line>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Line_pkey");

            entity.ToTable("Line", "zain-custom");

            entity.HasIndex(e => e.Uid, "Line_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Kitcode).HasColumnName("kitcode");
            entity.Property(e => e.LineType).HasColumnName("lineType");
            entity.Property(e => e.MarketType).HasColumnName("marketType");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Msisdn).HasColumnName("msisdn");
            entity.Property(e => e.PassportBarcode).HasColumnName("passportBarcode");
            entity.Property(e => e.ReferenceNumber).HasColumnName("referenceNumber");
            entity.Property(e => e.RequireReferenceNumber).HasColumnName("requireReferenceNumber");
            entity.Property(e => e.SimCard).HasColumnName("simCard");
            entity.Property(e => e.SimCardType).HasColumnName("simCardType");
            entity.Property(e => e.Uid).HasColumnName("uid");
        });

        modelBuilder.Entity<Link>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Link_pkey");

            entity.ToTable("Link", "zain");

            entity.HasIndex(e => e.Uid, "Link_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Content)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("content");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Scope).HasColumnName("scope");
            entity.Property(e => e.TokenId).HasColumnName("tokenId");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.Token).WithMany(p => p.Links)
                .HasForeignKey(d => d.TokenId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("Link_tokenId_fkey");
        });

        modelBuilder.Entity<Medium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Media_pkey");

            entity.ToTable("Media", "zain");

            entity.HasIndex(e => e.Uid, "Media_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArchiveId).HasColumnName("archiveId");
            entity.Property(e => e.Archived)
                .HasDefaultValue(false)
                .HasColumnName("archived");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Country).HasColumnName("country");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.DossierUid).HasColumnName("dossierUid");
            entity.Property(e => e.Extension).HasColumnName("extension");
            entity.Property(e => e.ExternalUid).HasColumnName("externalUid");
            entity.Property(e => e.Filename).HasColumnName("filename");
            entity.Property(e => e.MainDocument)
                .HasDefaultValue(false)
                .HasColumnName("mainDocument");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Path).HasColumnName("path");
            entity.Property(e => e.SentToArchive)
                .HasDefaultValue(false)
                .HasColumnName("sentToArchive");
            entity.Property(e => e.Side).HasColumnName("side");
            entity.Property(e => e.TemplateId).HasColumnName("templateId");
            entity.Property(e => e.TokenId).HasColumnName("tokenId");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.Url).HasColumnName("url");
            entity.Property(e => e.UserDocumentDataId).HasColumnName("userDocumentDataId");
            entity.Property(e => e.UserId).HasColumnName("userId");

            //entity.HasOne(d => d.Token).WithMany(p => p.Media)
            //    .HasForeignKey(d => d.TokenId)
            //    .OnDelete(DeleteBehavior.SetNull)
            //    .HasConstraintName("Media_tokenId_fkey");

            //entity.HasOne(d => d.UserDocumentData).WithMany(p => p.Media)
            //    .HasForeignKey(d => d.UserDocumentDataId)
            //    .OnDelete(DeleteBehavior.SetNull)
            //    .HasConstraintName("Media_userDocumentDataId_fkey");

            //entity.HasOne(d => d.User).WithMany(p => p.Media)
            //    .HasForeignKey(d => d.UserId)
            //    .OnDelete(DeleteBehavior.SetNull)
            //    .HasConstraintName("Media_userId_fkey");
        });

        modelBuilder.Entity<Mrzblacklist>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MRZBlacklist_pkey");

            entity.ToTable("MRZBlacklist", "zain-custom");

            entity.HasIndex(e => e.Mrz, "MRZBlacklist_mrz_key").IsUnique();

            entity.HasIndex(e => e.Uid, "MRZBlacklist_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Mrz).HasColumnName("mrz");
            entity.Property(e => e.SessionUid)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("sessionUid");
            entity.Property(e => e.Uid).HasColumnName("uid");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Package_pkey");

            entity.ToTable("Package", "zain-custom");

            entity.HasIndex(e => e.LineId, "Package_lineId_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArabicDescription).HasColumnName("arabicDescription");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DataPrice).HasColumnName("dataPrice");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.EnglishDescription).HasColumnName("englishDescription");
            entity.Property(e => e.GsmPrice).HasColumnName("gsmPrice");
            entity.Property(e => e.LineId).HasColumnName("lineId");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.PackageCode).HasColumnName("packageCode");
            entity.Property(e => e.PackageDetails)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("packageDetails");
            entity.Property(e => e.PackageId).HasColumnName("packageID");
            entity.Property(e => e.PromotionSubtitle).HasColumnName("promotionSubtitle");
            entity.Property(e => e.PromotionSubtitleAr).HasColumnName("promotionSubtitleAR");
            entity.Property(e => e.PromotionTitle).HasColumnName("promotionTitle");
            entity.Property(e => e.PromotionTitleAr).HasColumnName("promotionTitleAR");
            entity.Property(e => e.ServiceClass).HasColumnName("serviceClass");
            entity.Property(e => e.SwitchFee).HasColumnName("switchFee");

            entity.HasOne(d => d.Line).WithOne(p => p.Package)
                .HasForeignKey<Package>(d => d.LineId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Package_lineId_fkey");
        });

        modelBuilder.Entity<PrismaMigration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("_prisma_migrations_pkey");

            entity.ToTable("_prisma_migrations", "zain");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .HasColumnName("id");
            entity.Property(e => e.AppliedStepsCount)
                .HasDefaultValue(0)
                .HasColumnName("applied_steps_count");
            entity.Property(e => e.Checksum)
                .HasMaxLength(64)
                .HasColumnName("checksum");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.Logs).HasColumnName("logs");
            entity.Property(e => e.MigrationName)
                .HasMaxLength(255)
                .HasColumnName("migration_name");
            entity.Property(e => e.RolledBackAt).HasColumnName("rolled_back_at");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("started_at");
        });

        modelBuilder.Entity<PrismaMigration1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("_prisma_migrations_pkey");

            entity.ToTable("_prisma_migrations", "zain-custom");

            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .HasColumnName("id");
            entity.Property(e => e.AppliedStepsCount)
                .HasDefaultValue(0)
                .HasColumnName("applied_steps_count");
            entity.Property(e => e.Checksum)
                .HasMaxLength(64)
                .HasColumnName("checksum");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.Logs).HasColumnName("logs");
            entity.Property(e => e.MigrationName)
                .HasMaxLength(255)
                .HasColumnName("migration_name");
            entity.Property(e => e.RolledBackAt).HasColumnName("rolled_back_at");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("started_at");
        });

        modelBuilder.Entity<Recharge>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Recharge_pkey");

            entity.ToTable("Recharge", "zain-custom");

            entity.HasIndex(e => e.LineId, "Recharge_lineId_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.AmountWithTax).HasColumnName("amountWithTax");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.DeviceType).HasColumnName("deviceType");
            entity.Property(e => e.Hrn).HasColumnName("hrn");
            entity.Property(e => e.LineId).HasColumnName("lineId");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.PaymentMethod).HasColumnName("paymentMethod");
            entity.Property(e => e.PaymentToken).HasColumnName("paymentToken");
            entity.Property(e => e.ProductId).HasColumnName("productId");
            entity.Property(e => e.VoucherType).HasColumnName("voucherType");
            entity.Property(e => e.ZainCashWalletNumber).HasColumnName("zainCashWalletNumber");

            entity.HasOne(d => d.Line).WithOne(p => p.Recharge)
                .HasForeignKey<Recharge>(d => d.LineId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Recharge_lineId_fkey");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Session_pkey");

            entity.ToTable("Session", "zain");

            entity.HasIndex(e => e.TokenId, "Session_tokenId_key").IsUnique();

            entity.HasIndex(e => e.Uid, "Session_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AdaptedOcrData)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("adaptedOcrData");
            entity.Property(e => e.ArchiveAttempts)
                .HasDefaultValue(0)
                .HasColumnName("archiveAttempts");
            entity.Property(e => e.CheckArchiveAttempts)
                .HasDefaultValue(0)
                .HasColumnName("checkArchiveAttempts");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.DocumentAccepted)
                .HasDefaultValue(false)
                .HasColumnName("documentAccepted");
            entity.Property(e => e.DossierShareUid).HasColumnName("dossierShareUid");
            entity.Property(e => e.EmailConfirmed)
                .HasDefaultValue(false)
                .HasColumnName("emailConfirmed");
            entity.Property(e => e.FaceMatchThreshold).HasColumnName("faceMatchThreshold");
            entity.Property(e => e.FaceTemplates)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("faceTemplates");
            entity.Property(e => e.IdentificationAttempts)
                .HasDefaultValue(0)
                .HasColumnName("identificationAttempts");
            entity.Property(e => e.LivenessAccepted)
                .HasDefaultValue(false)
                .HasColumnName("livenessAccepted");
            entity.Property(e => e.LivenessOutput)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("livenessOutput");
            entity.Property(e => e.LivenessThreshold).HasColumnName("livenessThreshold");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.OcrData)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("ocrData");
            entity.Property(e => e.OcrDataAccepted)
                .HasDefaultValue(false)
                .HasColumnName("ocrDataAccepted");
            entity.Property(e => e.OtpEmail).HasColumnName("otpEmail");
            entity.Property(e => e.OtpPhone).HasColumnName("otpPhone");
            entity.Property(e => e.PhoneConfirmed)
                .HasDefaultValue(false)
                .HasColumnName("phoneConfirmed");
            entity.Property(e => e.PolicyAccepted)
                .HasDefaultValue(false)
                .HasColumnName("policyAccepted");
            entity.Property(e => e.RejectMotivation)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("rejectMotivation");
            entity.Property(e => e.SocketId).HasColumnName("socketId");
            entity.Property(e => e.TokenId).HasColumnName("tokenId");
            entity.Property(e => e.Tsa)
                .HasDefaultValue(false)
                .HasColumnName("tsa");
            entity.Property(e => e.TsaTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("tsaTime");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.Token).WithOne(p => p.Session)
                .HasForeignKey<Session>(d => d.TokenId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Session_tokenId_fkey");
        });

        modelBuilder.Entity<SessionComponent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SessionComponent_pkey");

            entity.ToTable("SessionComponent", "zain");

            entity.HasIndex(e => e.Uuid, "SessionComponent_uuid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Index).HasColumnName("index");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.SessionStepId).HasColumnName("sessionStepId");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.Uuid).HasColumnName("uuid");

            entity.HasOne(d => d.SessionStep).WithMany(p => p.SessionComponents)
                .HasForeignKey(d => d.SessionStepId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("SessionComponent_sessionStepId_fkey");
        });

        modelBuilder.Entity<SessionSection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SessionSection_pkey");

            entity.ToTable("SessionSection", "zain");

            entity.HasIndex(e => e.Uuid, "SessionSection_uuid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Content)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("content");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Hidden)
                .HasDefaultValue(false)
                .HasColumnName("hidden");
            entity.Property(e => e.Index).HasColumnName("index");
            entity.Property(e => e.MandatoryValidation)
                .HasDefaultValue(false)
                .HasColumnName("mandatoryValidation");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.SessionStepId).HasColumnName("sessionStepId");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.Uuid).HasColumnName("uuid");

            entity.HasOne(d => d.SessionStep).WithMany(p => p.SessionSections)
                .HasForeignKey(d => d.SessionStepId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("SessionSection_sessionStepId_fkey");
        });

        modelBuilder.Entity<SessionStep>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SessionStep_pkey");

            entity.ToTable("SessionStep", "zain");

            entity.HasIndex(e => e.Uuid, "SessionStep_uuid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Active)
                .HasDefaultValue(false)
                .HasColumnName("active");
            entity.Property(e => e.BodyActions)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("bodyActions");
            entity.Property(e => e.Completed)
                .HasDefaultValue(false)
                .HasColumnName("completed");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Dependencies)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("dependencies");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Disabled)
                .HasDefaultValue(false)
                .HasColumnName("disabled");
            entity.Property(e => e.Icon).HasColumnName("icon");
            entity.Property(e => e.Index).HasColumnName("index");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.PostActions)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("postActions");
            entity.Property(e => e.PreActions)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("preActions");
            entity.Property(e => e.Resume).HasColumnName("resume");
            entity.Property(e => e.Review)
                .HasDefaultValue(false)
                .HasColumnName("review");
            entity.Property(e => e.Route).HasColumnName("route");
            entity.Property(e => e.SessionWorkflowId).HasColumnName("sessionWorkflowId");
            entity.Property(e => e.Skip)
                .HasDefaultValue(false)
                .HasColumnName("skip");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.Uuid).HasColumnName("uuid");

            entity.HasOne(d => d.SessionWorkflow).WithMany(p => p.SessionSteps)
                .HasForeignKey(d => d.SessionWorkflowId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("SessionStep_sessionWorkflowId_fkey");
        });

        modelBuilder.Entity<SessionWorkflow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SessionWorkflow_pkey");

            entity.ToTable("SessionWorkflow", "zain");

            entity.HasIndex(e => e.SessionId, "SessionWorkflow_sessionId_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.SessionId).HasColumnName("sessionId");

            entity.HasOne(d => d.Session).WithOne(p => p.SessionWorkflow)
                .HasForeignKey<SessionWorkflow>(d => d.SessionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("SessionWorkflow_sessionId_fkey");
        });

        modelBuilder.Entity<Token>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Token_pkey");

            entity.ToTable("Token", "zain");

            entity.HasIndex(e => e.Token1, "Token_token_key").IsUnique();

            entity.HasIndex(e => e.Uid, "Token_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.FileData)
                .HasDefaultValueSql("'[]'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("fileData");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.SendEmail)
                .HasDefaultValue(false)
                .HasColumnName("sendEmail");
            entity.Property(e => e.SendSms)
                .HasDefaultValue(false)
                .HasColumnName("sendSms");
            entity.Property(e => e.Tenant).HasColumnName("tenant");
            entity.Property(e => e.Token1).HasColumnName("token");
            entity.Property(e => e.UiStyle).HasColumnName("uiStyle");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.UserCreationEmail).HasColumnName("userCreationEmail");
            entity.Property(e => e.UserCreationUid).HasColumnName("userCreationUid");
        });

        modelBuilder.Entity<TokenDocumentDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("TokenDocumentData_pkey");

            entity.ToTable("TokenDocumentData", "zain");

            entity.HasIndex(e => e.TokenId, "TokenDocumentData_tokenId_key").IsUnique();

            entity.HasIndex(e => e.Uid, "TokenDocumentData_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.ExpirationDate)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("expirationDate");
            entity.Property(e => e.IssuingCountry).HasColumnName("issuingCountry");
            entity.Property(e => e.IssuingDate)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("issuingDate");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Number).HasColumnName("number");
            entity.Property(e => e.TokenId).HasColumnName("tokenId");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.Token).WithOne(p => p.TokenDocumentDatum)
                .HasForeignKey<TokenDocumentDatum>(d => d.TokenId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("TokenDocumentData_tokenId_fkey");
        });

        modelBuilder.Entity<TokenUserAddressDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("TokenUserAddressData_pkey");

            entity.ToTable("TokenUserAddressData", "zain");

            entity.HasIndex(e => e.TokenId, "TokenUserAddressData_tokenId_key").IsUnique();

            entity.HasIndex(e => e.Uid, "TokenUserAddressData_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.City).HasColumnName("city");
            entity.Property(e => e.Country).HasColumnName("country");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.District).HasColumnName("district");
            entity.Property(e => e.Floor).HasColumnName("floor");
            entity.Property(e => e.FullAddress).HasColumnName("fullAddress");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Province).HasColumnName("province");
            entity.Property(e => e.StreetName).HasColumnName("streetName");
            entity.Property(e => e.StreetNumber).HasColumnName("streetNumber");
            entity.Property(e => e.TokenId).HasColumnName("tokenId");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.Unit).HasColumnName("unit");
            entity.Property(e => e.ZipCode).HasColumnName("zipCode");

            entity.HasOne(d => d.Token).WithOne(p => p.TokenUserAddressDatum)
                .HasForeignKey<TokenUserAddressDatum>(d => d.TokenId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("TokenUserAddressData_tokenId_fkey");
        });

        modelBuilder.Entity<TokenUserDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("TokenUserData_pkey");

            entity.ToTable("TokenUserData", "zain");

            entity.HasIndex(e => e.TokenId, "TokenUserData_tokenId_key").IsUnique();

            entity.HasIndex(e => e.Uid, "TokenUserData_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BirthCity).HasColumnName("birthCity");
            entity.Property(e => e.BirthCountry).HasColumnName("birthCountry");
            entity.Property(e => e.BirthDate)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("birthDate");
            entity.Property(e => e.BirthProvince).HasColumnName("birthProvince");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Firstname).HasColumnName("firstname");
            entity.Property(e => e.FiscalCode).HasColumnName("fiscalCode");
            entity.Property(e => e.Lastname).HasColumnName("lastname");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Nationality).HasColumnName("nationality");
            entity.Property(e => e.PhoneNumber).HasColumnName("phoneNumber");
            entity.Property(e => e.PhonePrefix).HasColumnName("phonePrefix");
            entity.Property(e => e.TokenId).HasColumnName("tokenId");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.Token).WithOne(p => p.TokenUserDatum)
                .HasForeignKey<TokenUserDatum>(d => d.TokenId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("TokenUserData_tokenId_fkey");
        });

        modelBuilder.Entity<TokenValidationDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("TokenValidationData_pkey");

            entity.ToTable("TokenValidationData", "zain");

            entity.HasIndex(e => e.Uid, "TokenValidationData_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CanBeEdited)
                .HasDefaultValue(true)
                .HasColumnName("canBeEdited");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Field).HasColumnName("field");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.OcrCheck)
                .HasDefaultValue(false)
                .HasColumnName("ocrCheck");
            entity.Property(e => e.TokenDocumentDataId).HasColumnName("tokenDocumentDataId");
            entity.Property(e => e.TokenUserAddressDataId).HasColumnName("tokenUserAddressDataId");
            entity.Property(e => e.TokenUserDataId).HasColumnName("tokenUserDataId");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.TokenDocumentData).WithMany(p => p.TokenValidationData)
                .HasForeignKey(d => d.TokenDocumentDataId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("TokenValidationData_tokenDocumentDataId_fkey");

            entity.HasOne(d => d.TokenUserAddressData).WithMany(p => p.TokenValidationData)
                .HasForeignKey(d => d.TokenUserAddressDataId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("TokenValidationData_tokenUserAddressDataId_fkey");

            entity.HasOne(d => d.TokenUserData).WithMany(p => p.TokenValidationData)
                .HasForeignKey(d => d.TokenUserDataId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("TokenValidationData_tokenUserDataId_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("User_pkey");

            entity.ToTable("User", "zain");

            entity.HasIndex(e => e.SessionId, "User_sessionId_key").IsUnique();

            entity.HasIndex(e => e.Uid, "User_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Age).HasColumnName("age");
            entity.Property(e => e.BirthCity).HasColumnName("birthCity");
            entity.Property(e => e.BirthCountry).HasColumnName("birthCountry");
            entity.Property(e => e.BirthDate)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("birthDate");
            entity.Property(e => e.BirthProvince).HasColumnName("birthProvince");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Firstname).HasColumnName("firstname");
            entity.Property(e => e.FiscalCode).HasColumnName("fiscalCode");
            entity.Property(e => e.Lastname).HasColumnName("lastname");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Nationality).HasColumnName("nationality");
            entity.Property(e => e.Parents).HasColumnName("parents");
            entity.Property(e => e.PhoneNumber).HasColumnName("phoneNumber");
            entity.Property(e => e.PhonePrefix).HasColumnName("phonePrefix");
            entity.Property(e => e.SessionId).HasColumnName("sessionId");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.Username).HasColumnName("username");

            entity.HasOne(d => d.Session).WithOne(p => p.User)
                .HasForeignKey<User>(d => d.SessionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("User_sessionId_fkey");
        });

        modelBuilder.Entity<UserAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("UserAddress_pkey");

            entity.ToTable("UserAddress", "zain");

            entity.HasIndex(e => e.Uid, "UserAddress_uid_key").IsUnique();

            entity.HasIndex(e => e.UserId, "UserAddress_userId_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.City).HasColumnName("city");
            entity.Property(e => e.Country).HasColumnName("country");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.District).HasColumnName("district");
            entity.Property(e => e.Floor).HasColumnName("floor");
            entity.Property(e => e.FullAddress).HasColumnName("fullAddress");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.Province).HasColumnName("province");
            entity.Property(e => e.StreetName).HasColumnName("streetName");
            entity.Property(e => e.StreetNumber).HasColumnName("streetNumber");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.Unit).HasColumnName("unit");
            entity.Property(e => e.UserId).HasColumnName("userId");
            entity.Property(e => e.ZipCode).HasColumnName("zipCode");

            entity.HasOne(d => d.User).WithOne(p => p.UserAddress)
                .HasForeignKey<UserAddress>(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("UserAddress_userId_fkey");
        });

        modelBuilder.Entity<UserDocumentDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("UserDocumentData_pkey");

            entity.ToTable("UserDocumentData", "zain");

            entity.HasIndex(e => e.Uid, "UserDocumentData_uid_key").IsUnique();

            entity.HasIndex(e => e.UserId, "UserDocumentData_userId_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.ExpirationDate)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("expirationDate");
            entity.Property(e => e.Expired)
                .HasDefaultValue(false)
                .HasColumnName("expired");
            entity.Property(e => e.IcaoDocumentFormat).HasColumnName("icaoDocumentFormat");
            entity.Property(e => e.IcaoDocumentType).HasColumnName("icaoDocumentType");
            entity.Property(e => e.IssuingCountry).HasColumnName("issuingCountry");
            entity.Property(e => e.IssuingDate)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("issuingDate");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.MrzCode).HasColumnName("mrzCode");
            entity.Property(e => e.Number).HasColumnName("number");
            entity.Property(e => e.OcrConfidence).HasColumnName("ocrConfidence");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.TypeLabel).HasColumnName("typeLabel");
            entity.Property(e => e.Uid).HasColumnName("uid");
            entity.Property(e => e.UserId).HasColumnName("userId");
            entity.Property(e => e.ValidColors)
                .HasDefaultValue(false)
                .HasColumnName("validColors");
            entity.Property(e => e.ValidDocument)
                .HasDefaultValue(false)
                .HasColumnName("validDocument");
            entity.Property(e => e.ValidLight)
                .HasDefaultValue(false)
                .HasColumnName("validLight");
            entity.Property(e => e.ValidMrzComposite)
                .HasDefaultValue(false)
                .HasColumnName("validMrzComposite");
            entity.Property(e => e.ValidMrzDateOfBirth)
                .HasDefaultValue(false)
                .HasColumnName("validMrzDateOfBirth");
            entity.Property(e => e.ValidMrzDateOfExpiry)
                .HasDefaultValue(false)
                .HasColumnName("validMrzDateOfExpiry");
            entity.Property(e => e.ValidMrzDocumentNumber)
                .HasDefaultValue(false)
                .HasColumnName("validMrzDocumentNumber");
            entity.Property(e => e.ValidNumber)
                .HasDefaultValue(false)
                .HasColumnName("validNumber");
            entity.Property(e => e.ValidSize)
                .HasDefaultValue(false)
                .HasColumnName("validSize");
            entity.Property(e => e.ValidTexture)
                .HasDefaultValue(false)
                .HasColumnName("validTexture");

            entity.HasOne(d => d.User).WithOne(p => p.UserDocumentDatum)
                .HasForeignKey<UserDocumentDatum>(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("UserDocumentData_userId_fkey");
        });

        modelBuilder.Entity<Validation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Validation_pkey");

            entity.ToTable("Validation", "zain");

            entity.HasIndex(e => e.SessionId, "Validation_sessionId_key").IsUnique();

            entity.HasIndex(e => e.Uid, "Validation_uid_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("creationTime");
            entity.Property(e => e.DeleteTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("deleteTime");
            entity.Property(e => e.Deleted)
                .HasDefaultValue(false)
                .HasColumnName("deleted");
            entity.Property(e => e.ModificationTime)
                .HasColumnType("timestamp(3) without time zone")
                .HasColumnName("modificationTime");
            entity.Property(e => e.SessionId).HasColumnName("sessionId");
            entity.Property(e => e.Token).HasColumnName("token");
            entity.Property(e => e.Uid).HasColumnName("uid");

            entity.HasOne(d => d.Session).WithOne(p => p.Validation)
                .HasForeignKey<Validation>(d => d.SessionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Validation_sessionId_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
