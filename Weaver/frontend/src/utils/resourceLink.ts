export function resourceLink(resourceType: string, resourceId: string): string | null {
  if (resourceType === "ScrapingProject") return `/scraping-projects/${resourceId}`;
  if (resourceType === "Workflow") return `/workflows/${resourceId}`;
  return null;
}
