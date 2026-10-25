# 🧬 Pandemic Shield - Bio-Security DNA Analysis System

![.NET](https://img.shields.io/badge/.NET-10.0%2B-512BD4?logo=dotnet)
![Flutter](https://img.shields.io/badge/Flutter-Desktop-02569B?logo=flutter)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Message%20Broker-FF6600?logo=rabbitmq)
![Docker](https://img.shields.io/badge/Docker-Containerized-2496ED?logo=docker)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Database-4169E1?logo=postgresql)

**Pandemic Shield** is an event-driven microservices system designed for real-time processing and analysis of massive genomic sequences (DNA/RNA). It acts as an automated bio-security scanner, detecting known pathogenic mutations (e.g., SARS-CoV-2) and human genetic markers (e.g., Sickle Cell Anemia) from `.fasta` files.

---

## 📸 System Interface

<img width="100%" alt="Dashboard View" src="https://github.com/user-attachments/assets/9b63b0a0-da2a-4f60-a70d-2f5146199da0" />

> *Dashboard View: Real-time threat polling and analytics.*

<img width="100%" alt="Dictionary View" src="https://github.com/user-attachments/assets/1fdb77aa-82ca-4774-9704-0be70304b05e" />

> *Dictionary View: CRUD operations for biological threat markers.*

---

## 🏗️ The Engineering Challenge & Solution

### The Problem: Memory Overload with Big Data
A standard human genome or large viral dataset `.fasta` file can exceed **3 GB** in plain text. Loading this directly into server RAM using traditional HTTP POST requests causes `OutOfMemoryException` and crashes standard monolithic backends.

### The Solution: Streaming & Event-Driven Architecture
This project implements a highly scalable pipeline to process gigabytes of data using a strict **4 GB RAM constraint** via Docker containers.

1. **gRPC Streaming:** The API ingests the massive file and immediately streams it in **32 KB chunks** via gRPC to the Parser service.
2. **Backpressure & Queuing:** The Parser translates the chunks into DTOs and publishes them to **RabbitMQ**. A deliberate backpressure mechanism (artificial delay) is implemented to prevent the message broker from flooding, balancing the ingestion rate with the processing rate.
3. **Record-aware windows:** The Parser parses FASTA records on the fly (headers, line breaks, case and IUPAC codes are handled) and cuts each record into overlapping windows, so nothing is lost at chunk boundaries and memory stays bounded.
4. **Distributed Workers:** Worker nodes consume windows asynchronously, find Open Reading Frames in all **6 reading frames** (both DNA strands) and scan the translated proteins against a PostgreSQL dictionary using Entity Framework Core.

---

## ⚙️ Architecture Diagram

### Microservices Breakdown:
* **API Gateway (ASP.NET Core Minimal APIs):** Handles HTTP Multipart file uploads and serves REST endpoints for the Flutter frontend.
* **Parser Service (gRPC):** Receives the binary stream, chunks the data, and publishes `DnaChunkMessage` events.
* **Bio library (`PandemicShield.Bio`):** Dependency-free biological core: streaming FASTA reader, IUPAC nucleotides, NCBI genetic code tables, overlapping windows, 6-frame ORF finder, protein motif scanner. Covered by golden tests on the SARS-CoV-2 reference genome.
* **Worker Service (.NET Worker):** Subscribes to RabbitMQ and runs the Bio library on each window (`MutationScannerService`).
* **Aggregator/DB Context:** EF Core implementation over PostgreSQL to store dictionaries and threat logs.
* **Frontend (Flutter Desktop):** A modern, clinical-themed desktop app with background auto-polling for real-time result synchronization.

---

## ✨ Key Features

* **Zero-Memory-Bloat Processing:** Safely handles 3GB+ files on constrained hardware.
* **Open Reading Frame (ORF) Detection:** 6-frame search (forward + reverse complement), longest ORF per stop codon like NCBI ORFfinder, nested/overlapping ORFs, minimum length, NCBI genetic code tables 1/2/4/11.
* **Genomic coordinates:** Findings are reported as `record:start-end (strand)`, 1-based like NCBI/GenBank, and linked to an analysis run (sample).
* **Live Sync UI:** Flutter desktop app automatically polls and renders detected threats in real-time without locking the UI thread.
* **Dynamic Threat Dictionary:** Full CRUD capabilities; workers reload the dictionary every 15 seconds, no restart needed.
* **Dockerized Environment:** One-command deployment `docker-compose up --build` spins up all services, RabbitMQ, and PostgreSQL.

---

## 🚀 Getting Started (Local Development)

### Prerequisites
* Docker Desktop
* Flutter SDK (for desktop compilation)
* .NET SDK 

### Running the Backend
1. Clone the repository.
2. Navigate to the root folder where `docker-compose.yml` is located.
3. Run the following command to spin up the cluster:
   ```bash
   docker-compose up --build -d
   ```
4. The API will be available at `http://localhost:5020`

> **Upgrading from an older version:** the database schema is now managed by EF Core migrations.
> If you ran the previous version, reset the database once with `docker-compose down` before `up`
> (the Postgres container has no volume, so this only removes old demo data).

### Running the tests
```bash
dotnet test PandemicShield.Bio.Tests
```

### Running the Frontend
1. Navigate to the `pandemic_shield_desktop` directory.
2. Run the Flutter app:
   ```bash
   flutter run -d windows  # (or macos/linux depending on your OS)
   ```

---

## 🧪 How to Test
Generate a realistic test file from the SARS-CoV-2 Wuhan-Hu-1 reference (MN908947) with the Spike D614G and Delta (E156G/Δ157-158) mutations, plus its reverse complement as a second record:
```bash
python3 docs/scripts/make_test_fasta.py > mutant.fasta
```
Upload `mutant.fasta` with category **Virus**. Expected findings (1-based):

| Record | Strand | Location | Marker |
|---|---|---|---|
| mutant_fwd | + | 21989-22054 | Delta: Spike E156G + del157-158 |
| mutant_fwd | + | 23375-23419 | Spike D614G |
| mutant_rc | - | 6479-6523 | Spike D614G |
| mutant_rc | - | 7844-7909 | Delta: Spike E156G + del157-158 |

The unmodified reference (`PandemicShield.Bio.Tests/Data/MN908947.fasta`) gives no findings.

> ⚠️ Pandemic Shield is a research and educational tool. It is **not** a medical device and must not be used for diagnosis.

---

## 📚 Documentation
* [`docs/DOMAIN_GUIDE.md`](docs/DOMAIN_GUIDE.md): biology primer (DNA, codons, ORFs, mutations, file formats) and how it maps to the code.
* [`docs/BIOLOGY_REVIEW.md`](docs/BIOLOGY_REVIEW.md): biological audit of the original implementation and field research.
* [`docs/DIRECTION.md`](docs/DIRECTION.md): where the project should go next and why.

---

## 🔮 Known Limitations & Future Roadmap

* **Motif matching is a simplified model.** Markers are exact amino-acid motifs inside ORF proteins. This misses synonymous/non-coding mutations and breaks when another mutation sits inside the motif. Proper variant calling means aligning to a reference (planned: Nextclade workers, markers as `gene:position ref>alt`).
* **ORF ≠ gene.** Programmed ribosomal frameshifts (e.g. coronavirus ORF1ab), spliced eukaryotic genes (introns) and real start sites are defined by genome annotation, not by ORF search.
* **Very long ORFs at window boundaries.** ORFs longer than the window flank (`WINDOW_FLANK_SIZE`, 32 768 nt by default) that cross a window boundary are not reported.
* **Input is consensus FASTA only.** FASTQ/BAM/VCF are not supported yet; FASTA cannot express diploid genotypes or mixed samples.
* **WebSockets/SignalR:** Upgrading the frontend polling mechanism to a true duplex WebSocket connection for real-time progress bars.
