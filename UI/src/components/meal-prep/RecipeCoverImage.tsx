import { useEffect, useState } from 'react';
import { recipeImageRequestUrl } from '@/lib/meal-prep';

interface RecipeCoverImageProps {
    workspaceId: string;
    recipeId: string;
    hasImage: boolean;
    className?: string;
    alt: string;
    /** Bumped after an upload so the new image is fetched instead of the cached one. */
    version?: number;
}

/**
 * Renders the recipe cover straight from the API URL rather than fetching it into a blob.
 *
 * The request is same-origin, so the session cookie is sent by the browser and the response is
 * stored in the HTTP cache: revisiting a recipe reuses the cached bytes, and once stale the
 * conditional request comes back as a 304 with no body. Fetching into an object URL bypassed all
 * of that and re-downloaded every image on every mount.
 */
export function RecipeCoverImage({
    workspaceId,
    recipeId,
    hasImage,
    className,
    alt,
    version,
}: RecipeCoverImageProps) {
    const [failed, setFailed] = useState(false);
    const src = recipeImageRequestUrl(workspaceId, recipeId, version);

    useEffect(() => {
        setFailed(false);
    }, [src]);

    if (!hasImage || failed) return null;

    return <img src={src} alt={alt} className={className} onError={() => setFailed(true)} />;
}
