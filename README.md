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
3. **Distributed Workers:** Worker nodes consume messages asynchronously, translating DNA codons into amino acid proteins (finding the Open Reading Frame via `ATG` start codons) and scanning them against a PostgreSQL dictionary using Entity Framework Core.

---

## ⚙️ Architecture Diagram

### Microservices Breakdown:
* **API Gateway (ASP.NET Core Minimal APIs):** Handles HTTP Multipart file uploads and serves REST endpoints for the Flutter frontend.
* **Parser Service (gRPC):** Receives the binary stream, chunks the data, and publishes `DnaChunkMessage` events.
* **Worker Service (.NET Worker):** Subscribes to RabbitMQ. Contains the core biological logic (`TranslationService` & `MutationScannerService`). 
* **Aggregator/DB Context:** EF Core implementation over PostgreSQL to store dictionaries and threat logs.
* **Frontend (Flutter Desktop):** A modern, clinical-themed desktop app with background auto-polling for real-time result synchronization.

---

## ✨ Key Features

* **Zero-Memory-Bloat Processing:** Safely handles 3GB+ files on constrained hardware.
* **Open Reading Frame (ORF) Detection:** Biologically accurate DNA-to-Protein translation.
* **Live Sync UI:** Flutter desktop app automatically polls and renders detected threats in real-time without locking the UI thread.
* **Dynamic Threat Dictionary:** Full CRUD capabilities to update the mutation database on the fly without restarting services.
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

### Running the Frontend
1. Navigate to the `pandemic_shield_desktop` directory.
2. Run the Flutter app:
   ```bash
   flutter run -d windows  # (or macos/linux depending on your OS)
   ```

---

## 🧪 How to Test
Upload a `.fasta` file through the UI. You can use standard viral references (e.g., SARS-CoV-2 isolate Wuhan-Hu-1) or create specific mutant `.fasta` files containing targeted markers like:
```text
ATGGTTTATTATCATAAAAATAATAAATCTTGGATGGAATCTGGTGTTTATTCTTCTGCTAATAATTGTTAA
```

---

## 🔮 Known Limitations & Future Roadmap

* **The Chunk Boundary Problem:** Currently, the parser strictly splits strings at 32 KB boundaries. If a biological mutation perfectly overlaps this physical boundary, it may be missed. Future iterations will implement an **Overlap Buffer (Sliding Window)** or stateful chunk processing (via Redis) to ensure no genetic sequences are split across messages.
* **WebSockets/SignalR:** Upgrading the frontend polling mechanism to a true duplex WebSocket connection for real-time progress bars.

