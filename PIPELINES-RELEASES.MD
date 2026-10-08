# Idea: Release Branches Tracker Page

**User Request:**
can we check in pipelines which repo is linked to the pipeline and which run of the pipeline was linked to deploy to production environment? my idea is to have a cross table of services/repos on rows, then in cols release branch that last went to prod and when, and current image version of docker and release in prod, and previous branch, previous image released to prod. That is used in case it needs to be rolled back. And this is a release branches tracker page. Don't do nothing, just tell me if it can be done, and if you think we can add other important information to help people here, programers and devops

---

**Feasibility Analysis:**
Yes, this can be done. The Azure DevOps REST API provides all the necessary endpoints to construct this release tracking matrix.

1. **Repo linked to pipeline:** 
   The Pipelines/Build Definitions API (`_apis/build/definitions`) returns the `repository` object containing the repo ID, name, and type (GitHub, Azure Repos, etc.).
2. **Pipeline run linked to production deployment:**
   - If using **Classic Releases**: The Release API (`_apis/release/deployments`) can be filtered by `environmentName` (e.g., "Production") and `deploymentStatus=succeeded`. The response includes the linked `buildId` (artifacts).
   - If using **YAML Multi-stage Pipelines**: The Environments API or Run Timeline API (`_apis/pipelines/{id}/runs`) tracks which stage (e.g., "Prod") succeeded.
3. **Branch and Docker Image Version:**
   The associated `buildId` contains `sourceBranch` (e.g., `refs/heads/release/v1.2`). The Docker image version is typically stored in the pipeline variables, build tags, or artifacts of that specific run, which can be extracted.
4. **Previous Branch/Image (Rollback targets):**
   By querying the top 2 most recent successful deployments to the "Production" environment, the system can extract the current state (index 0) and the previous state (index 1) to display rollback targets.

**Additional Recommended Data Points:**
To increase the utility of the Release Branches Tracker for developers and DevOps engineers, the following columns and data points can be integrated:

- **Commit Hash & Author:** The exact commit SHA deployed and the user who authored it.
- **Pull Request Links:** If the build was triggered by a PR merge, provide a direct link to the PR to quickly see what features/fixes are in the current production version.
- **Deployment Timestamp & Duration:** Exact time of the deployment and how long it took.
- **Pending Approvals Status:** A column indicating if a release is currently waiting for manual approval to enter the production environment.
- **Quick Rollback Link:** A generated deep link to the Azure DevOps portal to explicitly trigger a redeployment of the "Previous" version.
- **Health Check URL / Status:** A direct link to the live service's `/health` or `/metrics` endpoint.
- **Artifact/Package Links:** Direct links to the Azure Container Registry (ACR) or artifact feed where the specific Docker image is hosted.
