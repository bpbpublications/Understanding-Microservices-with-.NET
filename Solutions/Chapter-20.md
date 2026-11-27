# Chapter 20 · Real-world Examples and Case Studies

[All exercise solutions](README.md)

## 1. Promote one artifact

Build, test and scan one container image and record its digest. Reference the digest in each environment's manifest. Supply database endpoints, feature switches and identity at runtime. The staging and production pods then run the same program bits. Node architecture, runtime dependencies and configuration must still be compatible.

## 2. Serverless thumbnail generation

On ProductImageUploaded, queue the object key and version. A function reads the object, generates thumbnails and writes deterministic size/version keys. Publish ThumbnailReady so Catalog can update its projection. The queue absorbs bursts. Retry a transient storage error and dead-letter invalid files. Duplicate deliveries reuse the same output keys. Product upload need not wait for image processing.

## 3. RetailCo

In the book's case study, teams could release Catalog independently, moving from quarterly releases to several releases per week in some cases. They also scaled Order and Product individually for a holiday sale. Two costs were distributed debugging and service-to-service security. RetailCo introduced centralized logs with trace IDs, token validation and mutual TLS. Its operations team also had to tune resource limits and autoscaling.

## 4. Debug across boundaries

Track checkout success and p95, annotate releases and follow the trace for one failed checkout. Check whether the Order span ends in the database, a call to Inventory, or a broker publish. Search logs by trace and order ID for details. Inspect consumer lag if the HTTP request returned successfully but fulfillment is late. Verify the fix with the same user-facing metric and a new trace.
