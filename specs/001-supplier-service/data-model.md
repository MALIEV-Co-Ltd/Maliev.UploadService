# Data Model: Supplier Service

**Branch**: `001-supplier-service` | **Date**: 2025-11-22
**Phase**: 1 - Design & Contracts

## Entity Relationship Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SUPPLIER SERVICE                                │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────┐       ┌─────────────────────┐
│     Supplier        │       │  MaterialCategory   │
├─────────────────────┤       ├─────────────────────┤
│ PK Id (GUID)        │──┐    │ PK Id (GUID)        │
│ CompanyName         │  │    │ Name                │
│ TaxId (unique)      │  │    │ Description         │
│ Address             │  │    │ IsActive            │
│ City                │  │    │ CreatedAt           │
│ Country             │  │    │ UpdatedAt           │
│ PostalCode          │  │    └─────────────────────┘
│ Status              │  │              │
│ OnboardingStage     │  │              │ M:N
│ CreatedAt           │  │              ▼
│ UpdatedAt           │  │    ┌─────────────────────┐
│ RowVersion          │  │    │SupplierMaterialCat  │
└─────────────────────┘  │    ├─────────────────────┤
         │               │    │ FK SupplierId       │◄──┐
         │               │    │ FK MaterialCatId    │   │
         │ 1:N           └────┤                     │───┘
         │                    └─────────────────────┘
         │
         ├───────────────────┬───────────────────┬───────────────────┐
         │                   │                   │                   │
         ▼                   ▼                   ▼                   ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│SupplierContact  │  │SupplierCapability│  │SupplierCert     │  │PerformanceEval  │
├─────────────────┤  ├─────────────────┤  ├─────────────────┤  ├─────────────────┤
│ PK Id (GUID)    │  │ PK Id (GUID)    │  │ PK Id (GUID)    │  │ PK Id (GUID)    │
│ FK SupplierId   │  │ FK SupplierId   │  │ FK SupplierId   │  │ FK SupplierId   │
│ Name            │  │ Name            │  │ DocumentType    │  │ RatingCategory  │
│ Role            │  │ Description     │  │ DocumentName    │  │ Score (1-5)     │
│ Email           │  │ IsActive        │  │ IssueDate       │  │ EvaluationDate  │
│ Phone           │  │ CreatedAt       │  │ ExpirationDate  │  │ EvaluatorId     │
│ IsPrimary       │  │ UpdatedAt       │  │ ExternalFileRef │  │ EvaluatorName   │
│ CreatedAt       │  └─────────────────┘  │ CreatedAt       │  │ Notes           │
│ UpdatedAt       │                       │ UpdatedAt       │  │ CreatedAt       │
└─────────────────┘                       └─────────────────┘  └─────────────────┘
         │
         │ 1:N
         ▼
┌─────────────────┐         ┌─────────────────────┐
│OnboardingStatus │         │SupplierAuditLog     │
├─────────────────┤         ├─────────────────────┤
│ PK Id (GUID)    │         │ PK Id (GUID)        │
│ FK SupplierId   │         │ FK SupplierId       │
│ Stage           │         │ ChangeType          │
│ TransitionedAt  │         │ ChangedBy (userId)  │
│ TransitionedBy  │         │ ChangedByName       │
│ Notes           │         │ Timestamp           │
└─────────────────┘         │ EntityType          │
                            │ EntityId            │
                            │ OldValues (JSON)    │
                            │ NewValues (JSON)    │
                            └─────────────────────┘
```

## Entity Definitions

### 1. Supplier (Core Entity)

**Purpose**: The primary entity representing a supplier company.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PK, NOT NULL | Unique identifier |
| CompanyName | VARCHAR(200) | NOT NULL | Legal company name |
| TaxId | VARCHAR(50) | UNIQUE, NOT NULL | Tax identification number |
| Address | VARCHAR(500) | NOT NULL | Street address |
| City | VARCHAR(100) | NOT NULL | City name |
| Country | VARCHAR(100) | NOT NULL | Country name |
| PostalCode | VARCHAR(20) | NULL | Postal/ZIP code |
| Status | SMALLINT | NOT NULL, DEFAULT 0 | SupplierStatus enum |
| OnboardingStage | SMALLINT | NOT NULL, DEFAULT 0 | OnboardingStage enum |
| LastOrderDate | TIMESTAMP | NULL | Last purchase order date |
| CreatedAt | TIMESTAMP | NOT NULL | Creation timestamp (UTC) |
| UpdatedAt | TIMESTAMP | NOT NULL | Last update timestamp (UTC) |
| RowVersion | BYTEA | NOT NULL | Optimistic concurrency token |

**Indexes**:
- `PK_Suppliers` on `Id`
- `IX_Suppliers_TaxId` UNIQUE on `TaxId`
- `IX_Suppliers_Status` on `Status`
- `IX_Suppliers_CompanyName` on `CompanyName`
- `IX_Suppliers_OnboardingStage` on `OnboardingStage`

---

### 2. SupplierContact

**Purpose**: Contact information for individuals at a supplier company.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PK, NOT NULL | Unique identifier |
| SupplierId | UUID | FK, NOT NULL | Reference to Supplier |
| Name | VARCHAR(200) | NOT NULL | Contact full name |
| Role | VARCHAR(100) | NULL | Job title/role |
| Email | VARCHAR(255) | NOT NULL | Email address |
| Phone | VARCHAR(50) | NULL | Phone number |
| IsPrimary | BOOLEAN | NOT NULL, DEFAULT FALSE | Primary contact flag |
| CreatedAt | TIMESTAMP | NOT NULL | Creation timestamp (UTC) |
| UpdatedAt | TIMESTAMP | NOT NULL | Last update timestamp (UTC) |

**Indexes**:
- `PK_SupplierContacts` on `Id`
- `IX_SupplierContacts_SupplierId` on `SupplierId`
- `IX_SupplierContacts_Email` on `Email`

**Validation Rules**:
- Email must be valid format
- Only one primary contact per supplier (enforced in application)

---

### 3. MaterialCategory

**Purpose**: Lookup table for material categories suppliers can provide.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PK, NOT NULL | Unique identifier |
| Name | VARCHAR(100) | UNIQUE, NOT NULL | Category name |
| Description | VARCHAR(500) | NULL | Category description |
| IsActive | BOOLEAN | NOT NULL, DEFAULT TRUE | Active flag |
| CreatedAt | TIMESTAMP | NOT NULL | Creation timestamp (UTC) |
| UpdatedAt | TIMESTAMP | NOT NULL | Last update timestamp (UTC) |

**Indexes**:
- `PK_MaterialCategories` on `Id`
- `IX_MaterialCategories_Name` UNIQUE on `Name`
- `IX_MaterialCategories_IsActive` on `IsActive`

---

### 4. SupplierMaterialCategory (Join Table)

**Purpose**: Many-to-many relationship between Suppliers and MaterialCategories.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| SupplierId | UUID | PK, FK, NOT NULL | Reference to Supplier |
| MaterialCategoryId | UUID | PK, FK, NOT NULL | Reference to MaterialCategory |

**Indexes**:
- `PK_SupplierMaterialCategories` on `(SupplierId, MaterialCategoryId)`
- `IX_SupplierMaterialCategories_CategoryId` on `MaterialCategoryId`

---

### 5. SupplierCapability

**Purpose**: Specific capabilities or services a supplier offers.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PK, NOT NULL | Unique identifier |
| SupplierId | UUID | FK, NOT NULL | Reference to Supplier |
| Name | VARCHAR(200) | NOT NULL | Capability name |
| Description | VARCHAR(1000) | NULL | Detailed description |
| IsActive | BOOLEAN | NOT NULL, DEFAULT TRUE | Active flag |
| CreatedAt | TIMESTAMP | NOT NULL | Creation timestamp (UTC) |
| UpdatedAt | TIMESTAMP | NOT NULL | Last update timestamp (UTC) |

**Indexes**:
- `PK_SupplierCapabilities` on `Id`
- `IX_SupplierCapabilities_SupplierId` on `SupplierId`
- `IX_SupplierCapabilities_Name` on `Name`

---

### 6. SupplierCertification

**Purpose**: Metadata about certifications and compliance documents.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PK, NOT NULL | Unique identifier |
| SupplierId | UUID | FK, NOT NULL | Reference to Supplier |
| DocumentType | SMALLINT | NOT NULL | CertificationType enum |
| DocumentName | VARCHAR(200) | NOT NULL | Document display name |
| IssueDate | DATE | NOT NULL | Date document was issued |
| ExpirationDate | DATE | NULL | Expiration date (NULL = no expiry) |
| ExternalFileRef | VARCHAR(500) | NULL | Reference to external file storage |
| Notes | VARCHAR(1000) | NULL | Additional notes |
| CreatedAt | TIMESTAMP | NOT NULL | Creation timestamp (UTC) |
| UpdatedAt | TIMESTAMP | NOT NULL | Last update timestamp (UTC) |

**Indexes**:
- `PK_SupplierCertifications` on `Id`
- `IX_SupplierCertifications_SupplierId` on `SupplierId`
- `IX_SupplierCertifications_ExpirationDate` on `ExpirationDate`
- `IX_SupplierCertifications_DocumentType` on `DocumentType`

---

### 7. PerformanceEvaluation

**Purpose**: Historical record of supplier performance assessments.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PK, NOT NULL | Unique identifier |
| SupplierId | UUID | FK, NOT NULL | Reference to Supplier |
| RatingCategory | SMALLINT | NOT NULL | PerformanceRatingCategory enum |
| Score | SMALLINT | NOT NULL, CHECK (1-5) | Rating score (1=Poor, 5=Excellent) |
| EvaluationDate | DATE | NOT NULL | Date of evaluation |
| EvaluatorId | VARCHAR(100) | NOT NULL | User ID who performed evaluation |
| EvaluatorName | VARCHAR(200) | NOT NULL | User name for display |
| Notes | VARCHAR(2000) | NULL | Evaluation notes |
| CreatedAt | TIMESTAMP | NOT NULL | Creation timestamp (UTC) |

**Indexes**:
- `PK_PerformanceEvaluations` on `Id`
- `IX_PerformanceEvaluations_SupplierId` on `SupplierId`
- `IX_PerformanceEvaluations_EvaluationDate` on `EvaluationDate`
- `IX_PerformanceEvaluations_RatingCategory` on `RatingCategory`

**Validation Rules**:
- Score must be between 1 and 5 inclusive
- EvaluationDate cannot be in the future

---

### 8. SupplierAuditLog

**Purpose**: Immutable record of changes to supplier data (7-year retention).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PK, NOT NULL | Unique identifier |
| SupplierId | UUID | FK, NOT NULL | Reference to Supplier |
| ChangeType | VARCHAR(50) | NOT NULL | CREATE, UPDATE, DELETE |
| ChangedBy | VARCHAR(100) | NOT NULL | User ID who made change |
| ChangedByName | VARCHAR(200) | NOT NULL | User name for display |
| Timestamp | TIMESTAMP | NOT NULL | When change occurred (UTC) |
| EntityType | VARCHAR(100) | NOT NULL | Type of entity changed |
| EntityId | UUID | NOT NULL | ID of entity changed |
| OldValues | JSONB | NULL | Previous values (JSON) |
| NewValues | JSONB | NULL | New values (JSON) |

**Indexes**:
- `PK_SupplierAuditLogs` on `Id`
- `IX_SupplierAuditLogs_SupplierId` on `SupplierId`
- `IX_SupplierAuditLogs_Timestamp` on `Timestamp`
- `IX_SupplierAuditLogs_ChangedBy` on `ChangedBy`
- `IX_SupplierAuditLogs_EntityType_EntityId` on `(EntityType, EntityId)`

**Note**: This table is append-only. No UPDATE or DELETE operations permitted.

---

### 9. OnboardingStatus

**Purpose**: Tracks supplier progress through onboarding workflow stages.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PK, NOT NULL | Unique identifier |
| SupplierId | UUID | FK, NOT NULL | Reference to Supplier |
| Stage | SMALLINT | NOT NULL | OnboardingStage enum |
| TransitionedAt | TIMESTAMP | NOT NULL | When transition occurred (UTC) |
| TransitionedBy | VARCHAR(100) | NOT NULL | User ID who made transition |
| TransitionedByName | VARCHAR(200) | NOT NULL | User name for display |
| Notes | VARCHAR(1000) | NULL | Transition notes |

**Indexes**:
- `PK_OnboardingStatuses` on `Id`
- `IX_OnboardingStatuses_SupplierId` on `SupplierId`
- `IX_OnboardingStatuses_Stage` on `Stage`
- `IX_OnboardingStatuses_TransitionedAt` on `TransitionedAt`

---

## Enumerations

### SupplierStatus
```csharp
public enum SupplierStatus
{
    PendingApproval = 0,
    Active = 1,
    Suspended = 2,
    Inactive = 3
}
```

### OnboardingStage
```csharp
public enum OnboardingStage
{
    PendingApproval = 0,
    DocumentationReview = 1,
    FinalApproval = 2,
    Active = 3
}
```

### CertificationType
```csharp
public enum CertificationType
{
    TaxForm = 0,
    BusinessLicense = 1,
    RegulatoryCompliance = 2,
    QualityCertification = 3,
    InsuranceCertificate = 4,
    Other = 99
}
```

### PerformanceRatingCategory
```csharp
public enum PerformanceRatingCategory
{
    Quality = 0,
    Delivery = 1,
    Communication = 2,
    Pricing = 3,
    Overall = 4
}
```

---

## State Transitions

### Supplier Status Transitions

```
                    ┌──────────────────┐
                    │ PendingApproval  │
                    └────────┬─────────┘
                             │ (via onboarding completion)
                             ▼
                    ┌──────────────────┐
         ┌─────────│     Active       │─────────┐
         │         └────────┬─────────┘         │
         │                  │                   │
         │ (reactivate)     │ (suspend)         │ (deactivate)
         │                  ▼                   │
         │         ┌──────────────────┐         │
         └─────────│    Suspended     │         │
                   └────────┬─────────┘         │
                            │ (deactivate)      │
                            ▼                   ▼
                   ┌──────────────────────────────┐
                   │           Inactive           │
                   └──────────────────────────────┘
```

### Onboarding Stage Transitions

```
PendingApproval ──► DocumentationReview ──► FinalApproval ──► Active
```

**Valid Transitions**:
- `PendingApproval` → `DocumentationReview`
- `DocumentationReview` → `FinalApproval`
- `FinalApproval` → `Active` (also sets SupplierStatus to Active)

---

## Database Configuration

**Database Name**: `supplier_app_db`
**Schema**: `public` (default PostgreSQL schema)
**Collation**: `en_US.UTF-8`

### Table Naming Convention
- Tables: `snake_case` plural (e.g., `suppliers`, `supplier_contacts`)
- Columns: `snake_case` (e.g., `company_name`, `created_at`)
- Primary Keys: `id`
- Foreign Keys: `{entity}_id` (e.g., `supplier_id`)

### PostgreSQL-Specific Features
- JSONB for audit log old/new values (efficient querying)
- UUID for all primary keys
- TIMESTAMP WITH TIME ZONE for all dates
- BYTEA for RowVersion
