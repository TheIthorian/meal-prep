export interface HelpArticle {
    slug: string;
    title: string;
    description: string;
    readTime: string;
    audience: string;
}

export const helpArticles: HelpArticle[] = [
    {
        slug: 'getting-started',
        title: 'Getting started with MyApp',
        description: 'Set up your workspace, add your first accounts, and understand where to begin.',
        readTime: '4 min read',
        audience: 'New users',
    },
];
