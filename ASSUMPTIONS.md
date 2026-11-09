# Assumptions

Here are the assumptions made while developing this system:

1. **Student Identity:** The system assumes the X-Student-Id header contains a valid 17-digit registration number. No authentication/login system was implemented as per the instructions.
2. **Hold Duration:** The hold duration is set via the environment variable HOLD_DURATION_SECONDS. If not provided, it defaults to 600 seconds (10 minutes).
3. **Payment Gateway:** The "Confirm" endpoint simply simulates a successful payment. No real payment gateway integration was done.
4. **Expiration:** Expired holds are automatically marked as EXPIRED in the database whenever a new API request is made (lazy expiration). No background cron job was used.
5. **Concurrency Control:** A lock object is used in the backend controller to prevent multiple concurrent requests from overbooking seats.
6. **Multi-Process Concurrency:** To handle multiple backend processes (e.g., running on ports 8080 and 8081), we implemented database-level pessimistic locking using PostgreSQL's SELECT ... FOR UPDATE. This ensures that even if two separate processes try to book the last seat simultaneously, the database will lock the row, preventing overbooking.
