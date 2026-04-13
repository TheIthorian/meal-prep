import { useEffect, useState } from 'react';
import { recipeImageRequestUrl } from '@/lib/meal-prep';

interface RecipeCoverImageProps {
    workspaceId: string;
    recipeId: string;
    hasImage: boolean;
    className?: string;
    alt: string;
}

export function RecipeCoverImage({ workspaceId, recipeId, hasImage, className, alt }: RecipeCoverImageProps) {
    const [src, setSrc] = useState<string | null>(null);

    useEffect(() => {
        if (!hasImage) {
            setSrc(null);
            return;
        }

        let cancelled = false;
        let objectUrl: string | null = null;
        const url = recipeImageRequestUrl(workspaceId, recipeId);

        fetch(url, { credentials: 'include' })
            .then(res => {
                if (!res.ok) throw new Error('Failed to load image');
                return res.blob();
            })
            .then(blob => {
                if (cancelled) return;
                objectUrl = URL.createObjectURL(blob);
                setSrc(objectUrl);
            })
            .catch(() => {
                if (!cancelled) setSrc(null);
            });

        return () => {
            cancelled = true;
            if (objectUrl) URL.revokeObjectURL(objectUrl);
        };
    }, [hasImage, workspaceId, recipeId]);

    if (!hasImage || !src) return null;

    return <img src={src} alt={alt} className={className} />;
}
