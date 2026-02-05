import { ModlistMetadata, Repositories, API_BASE_URL } from './types';

async function fetchJson<T>(url: string): Promise<T> {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`HTTP error! status: ${response.status}`);
  }
  return response.json();
}

function buildApiUrl(path: string): string {
  return `${API_BASE_URL}${path.startsWith('/') ? '' : '/'}${path}`;
}

export async function fetchRepositories(): Promise<Repositories> {
  return fetchJson<Repositories>(buildApiUrl('/repositories.json'));
}

export async function fetchModlistsFromRepo(repoUrl: string): Promise<ModlistMetadata[]> {
  const data = await fetchJson<ModlistMetadata | ModlistMetadata[]>(repoUrl);
  // Some repos return a single object, others return an array
  return Array.isArray(data) ? data : [data];
}

export async function fetchAllModlists(): Promise<ModlistMetadata[]> {
  const repositories = await fetchRepositories();

  const modlistPromises = Object.entries(repositories).map(async ([repoName, repoUrl]) => {
    try {
      const modlists = await fetchModlistsFromRepo(repoUrl);
      // Enrich with repository name and namespaced name
      return modlists.map(m => {
        const namespacedName = `${repoName}/${m.links.machineURL}`;
        return {
          ...m,
          repositoryName: repoName,
          namespacedName,
          // Mark as official if from wj-featured repo
          official: repoName === 'wj-featured' || m.official,
        };
      });
    } catch (error) {
      console.warn(`Failed to fetch modlists from ${repoName}:`, error);
      return [];
    }
  });

  const results = await Promise.all(modlistPromises);
  return results.flat();
}
