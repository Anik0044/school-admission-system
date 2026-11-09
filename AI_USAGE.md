# AI Usage

During the development of this project, AI tools (such as ChatGPT/Claude) were used for the following purposes:

1. **Boilerplate Code:** AI was used to generate the initial structure of the .NET API controllers and React components.
2. **Docker Configuration:** AI helped write the Dockerfile and docker-compose.yml files to ensure seamless containerization.
3. **Debugging:** AI was used to troubleshoot port conflicts (e.g., fixing the PostgreSQL port 5432 conflict by changing internal ports) and CORS issues.
4. **Testing:** AI assisted in writing the Concurrency Test using xUnit.

**Corrections Made:** AI initially provided a Concurrency Test without locking, which failed. I corrected it by adding a lock object to simulate real-world thread safety, which made the test pass successfully.
