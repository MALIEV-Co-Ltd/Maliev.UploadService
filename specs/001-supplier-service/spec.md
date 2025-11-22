# Feature Specification: Supplier Service WebAPI

**Feature Branch**: `001-supplier-service`
**Created**: 2025-11-21
**Status**: Draft
**Input**: User description: "Create a Supplier Service WebAPI for MALIEV. The Supplier Service is responsible for managing all supplier-related data and processes within the MALIEV microservices ecosystem."

## Clarifications

### Session 2025-11-22

- Q: What scale should be used for performance ratings? → A: 1-5 integer scale (simple, widely understood)
- Q: How long should audit logs be retained? → A: 7 years (standard financial/procurement compliance period)
- Q: How should cross-service dependency checks work for supplier deletion? → A: Synchronous API calls to dependent services at deletion time

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register New Supplier (Priority: P1)

As a procurement administrator, I need to register new suppliers in the system so that they can be referenced in purchase orders and other procurement activities.

**Why this priority**: This is the foundational capability - without the ability to create suppliers, no other supplier-related operations are possible. This enables the basic data entry that all other features depend on.

**Independent Test**: Can be fully tested by creating a new supplier record with company profile, contact information, and material categories, then verifying the supplier appears in the system and can be retrieved.

**Acceptance Scenarios**:

1. **Given** I am an authenticated procurement administrator, **When** I submit a complete supplier registration form with company name, contact details, and material categories, **Then** a new supplier record is created with status "Pending Approval" and a unique identifier is assigned.
2. **Given** I am registering a new supplier, **When** I submit the form with missing required fields, **Then** the system returns validation errors indicating which fields are missing.
3. **Given** I am registering a new supplier, **When** I submit a supplier with a duplicate tax identification number, **Then** the system rejects the registration and indicates the conflict.

---

### User Story 2 - Retrieve Supplier Information (Priority: P1)

As a purchase order service or procurement user, I need to retrieve supplier information so that I can validate suppliers and populate purchase order forms with current supplier data.

**Why this priority**: Essential for integration with Purchase Order Service - this enables the primary consumption pattern where other services validate and retrieve supplier data.

**Independent Test**: Can be fully tested by querying for a supplier by ID or by searching for suppliers by criteria (name, status, category) and verifying accurate data is returned quickly.

**Acceptance Scenarios**:

1. **Given** a supplier exists in the system, **When** I request supplier details by ID, **Then** I receive the complete supplier profile including company information, contacts, capabilities, certifications, and current status.
2. **Given** multiple suppliers exist, **When** I search for suppliers by material category, **Then** I receive a list of active suppliers that supply materials in that category.
3. **Given** a supplier does not exist, **When** I request supplier details by a non-existent ID, **Then** I receive a clear "not found" response.

---

### User Story 3 - Update Supplier Information (Priority: P2)

As a procurement administrator, I need to update supplier information so that the system reflects current supplier details, capabilities, and status.

**Why this priority**: Once suppliers exist, maintaining accurate data is critical for ongoing operations. This enables the day-to-day management of supplier records.

**Independent Test**: Can be fully tested by modifying supplier fields (contact info, capabilities, status) and verifying the changes are persisted and an audit trail is created.

**Acceptance Scenarios**:

1. **Given** an existing supplier record, **When** I update the contact information, **Then** the changes are saved and an audit entry is created recording who made the change and when.
2. **Given** an existing supplier record, **When** I update the operational status to "Suspended", **Then** the supplier is marked as suspended and this change is reflected in eligibility queries.
3. **Given** an existing supplier record, **When** I attempt to update with invalid data, **Then** validation errors are returned and no changes are made.

---

### User Story 4 - List Active Suppliers (Priority: P2)

As a procurement user, I need to list all active suppliers, optionally filtered by criteria, so that I can select appropriate suppliers for purchase orders.

**Why this priority**: This supports the common workflow of browsing and selecting suppliers during procurement activities.

**Independent Test**: Can be fully tested by retrieving a paginated list of suppliers with various filters (status, category, capability) and verifying correct results are returned with acceptable performance.

**Acceptance Scenarios**:

1. **Given** multiple suppliers exist with different statuses, **When** I request a list of active suppliers, **Then** I receive only suppliers with "Active" status in a paginated format.
2. **Given** multiple suppliers exist, **When** I filter by material category "Electronics", **Then** I receive only suppliers that supply electronics materials.
3. **Given** a large number of suppliers exist, **When** I request the first page of results, **Then** I receive results within acceptable response time with pagination metadata.

---

### User Story 5 - Manage Supplier Documentation Metadata (Priority: P2)

As a procurement administrator, I need to track documentation requirements and metadata for suppliers (certifications, tax forms, compliance declarations) so that I can ensure suppliers meet regulatory requirements.

**Why this priority**: Compliance tracking is essential for supplier qualification. While actual files are stored externally, metadata must be managed locally.

**Independent Test**: Can be fully tested by adding document metadata records to a supplier, tracking expiration dates, and querying for suppliers with missing or expired documents.

**Acceptance Scenarios**:

1. **Given** an existing supplier, **When** I add a certification document metadata record with name, type, expiration date, and external file reference, **Then** the metadata is associated with the supplier.
2. **Given** a supplier has certification metadata with an expiration date, **When** I query for suppliers with expiring certifications in the next 30 days, **Then** I receive a list of suppliers requiring attention.
3. **Given** a supplier has multiple document requirements, **When** I view the supplier's compliance status, **Then** I see which documents are present, missing, or expired.

---

### User Story 6 - Record Performance Ratings (Priority: P3)

As a procurement administrator, I need to record and view historical performance ratings for suppliers so that I can make informed decisions about supplier selection.

**Why this priority**: Performance tracking enhances procurement decisions but is not essential for basic operations.

**Independent Test**: Can be fully tested by adding performance evaluation records to a supplier and querying the supplier's performance history.

**Acceptance Scenarios**:

1. **Given** an existing supplier, **When** I submit a performance evaluation with rating category, score, evaluation date, and notes, **Then** the evaluation is recorded in the supplier's history.
2. **Given** a supplier has multiple performance evaluations, **When** I view the supplier's performance history, **Then** I see all evaluations sorted by date with summary statistics.
3. **Given** I need to compare suppliers, **When** I request suppliers sorted by average performance rating, **Then** I receive suppliers ranked by their aggregate performance scores.

---

### User Story 7 - Track Supplier Onboarding Workflow (Priority: P3)

As a procurement administrator, I need to track the onboarding status of new suppliers through defined workflow stages so that I can ensure all qualification steps are completed.

**Why this priority**: Formal onboarding workflows ensure consistency but can be managed manually initially.

**Independent Test**: Can be fully tested by advancing a supplier through onboarding stages and verifying status transitions are recorded with timestamps.

**Acceptance Scenarios**:

1. **Given** a newly registered supplier with "Pending Approval" status, **When** I advance the onboarding to "Documentation Review", **Then** the status is updated and the transition is logged with timestamp.
2. **Given** a supplier in "Documentation Review" stage, **When** all required documents are verified, **Then** I can advance to "Final Approval" stage.
3. **Given** a supplier has completed all onboarding stages, **When** final approval is granted, **Then** the supplier status changes to "Active" and is eligible for purchase orders.

---

### User Story 8 - Audit Trail Access (Priority: P3)

As an auditor or compliance officer, I need to view the complete audit trail for any supplier so that I can verify compliance and investigate changes.

**Why this priority**: Audit capabilities support compliance requirements but the core audit logging happens automatically with other operations.

**Independent Test**: Can be fully tested by making changes to a supplier and then retrieving the audit log showing all modifications.

**Acceptance Scenarios**:

1. **Given** a supplier has been modified multiple times, **When** I request the audit trail for that supplier, **Then** I see all changes with timestamps, user identifiers, and before/after values.
2. **Given** I need to investigate a specific time period, **When** I filter the audit trail by date range, **Then** I see only changes within that period.

---

### Edge Cases

- What happens when attempting to delete a supplier that is referenced by existing purchase orders? The system must prevent deletion and return a clear error explaining the dependency.
- What happens when attempting to delete a supplier referenced by invoices or stock transactions? Same protection applies - deletion is blocked with explanation of blocking references.
- What happens when a supplier's certification expires? The system should allow querying for expired certifications but not automatically change supplier status (manual review required).
- What happens when updating a supplier while another user is editing? Optimistic concurrency control should detect conflicts and prevent data loss.
- What happens when the external document storage service is unavailable? Document metadata operations should still succeed; only the file reference is stored locally.
- What happens when retrieving a supplier with a very large audit history? Audit trail queries should be paginated to maintain performance.
- What happens when a dependent service (Purchase Order, Invoice, Stock) is unavailable during a deletion attempt? The system must fail the deletion safely (fail-closed) and return an error indicating which service(s) could not be reached, preventing accidental deletion of referenced suppliers.

## Requirements *(mandatory)*

### Functional Requirements

**Core CRUD Operations**

- **FR-001**: System MUST allow creation of new supplier records with company profile, contact information, material categories, capabilities, and initial status.
- **FR-002**: System MUST assign a unique identifier to each supplier upon creation.
- **FR-003**: System MUST allow retrieval of supplier information by unique identifier.
- **FR-004**: System MUST allow updating of supplier information with partial updates supported.
- **FR-005**: System MUST prevent deletion of suppliers that are referenced by purchase orders, invoices, or stock transactions. Verification is performed via synchronous API calls to dependent services (Purchase Order Service, Invoice Service, Stock Service) at deletion time to ensure real-time accuracy.
- **FR-005a**: System MUST fail the deletion operation safely if any dependent service is unavailable during the reference check (fail-closed behavior).
- **FR-006**: System MUST return clear error messages indicating blocking references when deletion is prevented, including which service(s) reported active references.

**Search and Listing**

- **FR-007**: System MUST support listing suppliers with pagination.
- **FR-008**: System MUST support filtering suppliers by status (Active, Suspended, Pending Approval, Inactive).
- **FR-009**: System MUST support filtering suppliers by material categories supplied.
- **FR-010**: System MUST support filtering suppliers by capabilities.
- **FR-011**: System MUST support sorting suppliers by name, creation date, or performance rating.

**Documentation and Compliance**

- **FR-012**: System MUST allow recording of document metadata (type, name, expiration date, external file reference) for suppliers.
- **FR-013**: System MUST track certification types including tax forms, regulatory compliance declarations, and business certifications.
- **FR-014**: System MUST support querying for suppliers with expiring or expired certifications.
- **FR-015**: System MUST store only document metadata locally; actual file storage is delegated to external document management service.

**Performance and Ratings**

- **FR-016**: System MUST allow recording of performance evaluation records with rating category, score, date, and notes.
- **FR-017**: System MUST maintain historical performance ratings for each supplier.
- **FR-018**: System MUST calculate and expose aggregate performance metrics (average rating, number of evaluations).

**Audit and History**

- **FR-019**: System MUST automatically log all changes to supplier records including the user who made the change, timestamp, and changed values.
- **FR-020**: System MUST provide queryable audit trail for each supplier.
- **FR-021**: System MUST support filtering audit logs by date range.
- **FR-022**: Audit logs MUST be immutable once created.
- **FR-022a**: Audit logs MUST be retained for a minimum of 7 years to satisfy financial and procurement compliance requirements.

**Onboarding Workflow**

- **FR-023**: System MUST support tracking supplier onboarding through defined stages (Pending Approval, Documentation Review, Final Approval, Active).
- **FR-024**: System MUST record timestamps for each onboarding stage transition.
- **FR-025**: System MUST enforce valid stage transitions (no skipping required stages).

**Data Integrity**

- **FR-026**: System MUST validate required fields on supplier creation and update.
- **FR-027**: System MUST prevent duplicate suppliers based on tax identification number.
- **FR-028**: System MUST implement optimistic concurrency control to prevent conflicting updates.

**Caching and Performance**

- **FR-029**: System MUST implement caching for frequently accessed data (active supplier lists, supplier details).
- **FR-030**: System MUST invalidate cache entries when underlying data changes.
- **FR-031**: System MUST support cache bypass for operations requiring real-time data.

**Integration Support**

- **FR-032**: System MUST expose supplier validation endpoint for use by Purchase Order Service.
- **FR-033**: System MUST expose endpoint to check supplier eligibility for purchase orders (active status, valid certifications).
- **FR-034**: System MUST support updating supplier metadata from procurement operations (e.g., last order date).

### Key Entities

- **Supplier**: The core entity representing a supplier company. Contains company profile (name, tax ID, address), operational status, creation and modification timestamps.
- **SupplierContact**: Contact information for individuals at a supplier company. Contains name, role, email, phone. A supplier may have multiple contacts.
- **MaterialCategory**: Categories of materials a supplier can provide. Suppliers can be associated with multiple categories.
- **SupplierCapability**: Specific capabilities or services a supplier offers. Used for matching suppliers to requirements.
- **SupplierCertification**: Metadata about certifications and compliance documents. Contains document type, name, issue date, expiration date, and external file reference.
- **PerformanceEvaluation**: Historical record of supplier performance assessments. Contains rating category, score, evaluation date, evaluator, and notes.
- **SupplierAuditLog**: Immutable record of changes to supplier data. Contains supplier reference, change type, timestamp, user, and before/after values.
- **OnboardingStatus**: Tracks supplier progress through onboarding workflow stages with timestamps for each transition.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Procurement administrators can register a new supplier with complete profile information in under 5 minutes.
- **SC-002**: Supplier information retrieval by ID returns results in under 500 milliseconds for 99% of requests.
- **SC-003**: Active supplier list queries return results in under 1 second for lists up to 1000 suppliers.
- **SC-004**: System supports at least 100 concurrent users performing supplier operations without degradation.
- **SC-005**: 100% of supplier data modifications are captured in audit logs with no gaps.
- **SC-006**: Suppliers with blocking references (purchase orders, invoices, stock) cannot be deleted under any circumstances.
- **SC-007**: Cache hit rate for active supplier queries exceeds 80% during normal operations.
- **SC-008**: Purchase Order Service can validate supplier eligibility in under 200 milliseconds.
- **SC-009**: Users can identify suppliers with expiring certifications (within 30 days) through a single query.
- **SC-010**: All supplier status transitions are recorded with accurate timestamps and user attribution.

## Assumptions

- Authentication and authorization are handled by an existing identity service; this service will receive authenticated requests with user context.
- The external document management/upload service exists and provides a mechanism to store files and return references.
- Other microservices (Purchase Order Service, Invoice Service, Stock Service) exist or will exist and can be queried to check for supplier references before deletion.
- Standard pagination defaults (page size of 20-50 items) are acceptable unless otherwise configured.
- Onboarding workflow stages are fixed as specified; custom workflows are out of scope for initial implementation.
- Performance ratings use a 1-5 integer scale where 1 = Poor and 5 = Excellent.
