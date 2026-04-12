/**
 * HTTP validation problem details model
 */
export interface HttpValidationProblemDetails {
    type?: string | null;
    title?: string | null;
    status?: number | null;
    detail?: string | null;
    instance?: string | null;
    errors?: Record<string, string[]> | null;
    [key: string]: unknown;
}
