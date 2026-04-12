/**
 * Access token response model
 */
export interface AccessTokenResponse {
    tokenType: string;
    accessToken: string;
    expiresIn: number;
    refreshToken: string;
}

/**
 * Login request model
 */
export interface LoginRequest {
    email: string;
    password: string;
    twoFactorCode?: string | null;
    twoFactorRecoveryCode?: string | null;
}

/**
 * Register request model
 */
export interface RegisterRequest {
    email: string;
    password: string;
    displayName?: string;
}

/**
 * Refresh request model
 */
export interface RefreshRequest {
    refreshToken: string;
}

/**
 * Forgot password request model
 */
export interface ForgotPasswordRequest {
    email: string;
}

/**
 * Reset password request model
 */
export interface ResetPasswordRequest {
    email: string;
    resetCode: string;
    newPassword: string;
}

/**
 * Resend confirmation email request model
 */
export interface ResendConfirmationEmailRequest {
    email: string;
}

/**
 * Two factor request model
 */
export interface TwoFactorRequest {
    enable?: boolean | null;
    twoFactorCode?: string | null;
    resetSharedKey: boolean;
    resetRecoveryCodes: boolean;
    forgetMachine: boolean;
}

/**
 * Two factor response model
 */
export interface TwoFactorResponse {
    sharedKey?: string | null;
    recoveryCodesLeft: number;
    recoveryCodes?: string[] | null;
    isTwoFactorEnabled: boolean;
    isMachineRemembered: boolean;
}
