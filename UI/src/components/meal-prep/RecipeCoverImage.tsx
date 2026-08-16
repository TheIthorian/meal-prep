import { useEffect, useState } from 'react';
import { recipeImageRequestUrl, recipeImageSrcSet } from '@/lib/meal-prep';

interface RecipeCoverImageProps {
    workspaceId: string;
    recipeId: string;
    hasImage: boolean;
    className?: string;
    alt: string;
    /** Bumped after an upload so the new image is fetched instead of the cached one. */
    version?: number;
    /**
     * How wide the image renders, as a `sizes` attribute. Without it the browser assumes the
     * image fills the viewport and picks the largest rendition on every screen.
     */
    sizes?: string;
    /**
     * Set on the image most likely to be the largest contentful paint — the first card in a grid,
     * or a detail page hero. Such an image is fetched eagerly at high priority; every other one is
     * left to load lazily.
     */
    priority?: boolean;
}

/**
 * Renders the recipe cover straight from the API URL rather than fetching it into a blob.
 *
 * The request is same-origin, so the session cookie is sent by the browser and the response is
 * stored in the HTTP cache: revisiting a recipe reuses the cached bytes, and once stale the
 * conditional request comes back as a 304 with no body. Fetching into an object URL bypassed all
 * of that and re-downloaded every image on every mount.
 *
 * The API stores each image at several widths, offered here as a srcset so a card does not
 * download a full-size image to render it at a few hundred pixels.
 */
export function RecipeCoverImage({
    workspaceId,
    recipeId,
    hasImage,
    className,
    alt,
    version,
    sizes,
    priority = false,
}: RecipeCoverImageProps) {
    const [failed, setFailed] = useState(false);
    const src = recipeImageRequestUrl(workspaceId, recipeId, version);
    const srcSet = recipeImageSrcSet(workspaceId, recipeId, version);

    useEffect(() => {
        setFailed(false);
    }, [src]);

    if (!hasImage || failed) return null;

    return (
        <img
            src={src}
            srcSet={srcSet}
            sizes={sizes}
            alt={alt}
            className={className}
            loading={priority ? 'eager' : 'lazy'}
            fetchPriority={priority ? 'high' : 'auto'}
            decoding='async'
            onError={() => setFailed(true)}
        />
    );
}
