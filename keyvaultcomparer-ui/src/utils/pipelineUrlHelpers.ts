import type { PipelineRepository } from '../stores/devopsDataStore';

export function getPipelineDefinitionUrl(organization: string, project: string, pipelineId: number): string {
  return `https://dev.azure.com/${organization}/${project}/_build?definitionId=${pipelineId}`;
}

export function getRepoUrl(
  repo?: PipelineRepository,
  githubBaseUrl?: string,
  useGithubOrg?: boolean,
  githubOrg?: string
): string | null {
  if (!repo) return null;
  if (repo.url) return repo.url;
  if (repo.type?.toLowerCase() === 'github') {
    const baseUrl = githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    let repoName = repo.name || '';
    if (!repoName.includes('/') && useGithubOrg && githubOrg) {
      repoName = `${githubOrg}/${repoName}`;
    }
    return `${baseUrl}/${repoName}`;
  }
  return null;
}

export function getCommitUrl(
  repo?: PipelineRepository,
  commitSha?: string,
  organization?: string,
  project?: string,
  githubBaseUrl?: string,
  useGithubOrg?: boolean,
  githubOrg?: string
): string | null {
  if (!commitSha || !repo) return null;

  if (repo.type?.toLowerCase() === 'github' || repo.url?.includes('github.com')) {
    const baseUrl = githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    let repoName = repo.name || '';
    if (!repoName.includes('/') && useGithubOrg && githubOrg) {
      repoName = `${githubOrg}/${repoName}`;
    }
    return `${baseUrl}/${repoName}/commit/${commitSha}`;
  } else if (repo.type?.toLowerCase() === 'tfsgit' || repo.url?.includes('_git')) {
    if (repo.url) {
      return `${repo.url}/commit/${commitSha}`;
    }
    if (organization && project) {
      return `https://dev.azure.com/${organization}/${project}/_git/${repo.name}/commit/${commitSha}`;
    }
  }
  return null;
}

export function getPrUrl(
  repo?: PipelineRepository,
  prNumber?: string,
  organization?: string,
  project?: string,
  githubBaseUrl?: string,
  useGithubOrg?: boolean,
  githubOrg?: string
): string | null {
  if (!prNumber || !repo) return null;

  if (repo.type?.toLowerCase() === 'github' || repo.url?.includes('github.com')) {
    const baseUrl = githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    let repoName = repo.name || '';
    if (!repoName.includes('/') && useGithubOrg && githubOrg) {
      repoName = `${githubOrg}/${repoName}`;
    }
    return `${baseUrl}/${repoName}/pull/${prNumber}`;
  } else if (repo.type?.toLowerCase() === 'tfsgit' || repo.url?.includes('_git')) {
    if (repo.url) {
      return `${repo.url}/pullrequest/${prNumber}`;
    }
    if (organization && project) {
      return `https://dev.azure.com/${organization}/${project}/_git/${repo.name}/pullrequest/${prNumber}`;
    }
  }
  return null;
}

export function formatTimeAgo(iso?: string): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (isNaN(date.getTime())) return '';
  const diffSec = Math.floor((Date.now() - date.getTime()) / 1000);
  if (diffSec < 60) return 'just now';
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHours = Math.floor(diffMin / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays}d ago`;
}

export function getBuildUrl(
  organization?: string,
  project?: string,
  buildId?: number,
  fallbackUrl?: string
): string | null {
  if (fallbackUrl) return fallbackUrl;
  if (organization && project && buildId) {
    return `https://dev.azure.com/${organization}/${project}/_build/results?buildId=${buildId}`;
  }
  return null;
}

