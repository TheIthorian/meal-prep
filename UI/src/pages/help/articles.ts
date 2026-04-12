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
        title: 'Getting started with Meal Prep',
        description: 'Set up your workspace, save your first recipe, and start building shopping lists.',
        readTime: '4 min read',
        audience: 'New users',
    },
];
