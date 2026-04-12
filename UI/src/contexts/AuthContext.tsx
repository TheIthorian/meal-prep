import { createContext, useContext, useState, useEffect, ReactNode, useCallback } from 'react';
import { authApi } from '@/lib/api';
import type { UserResponse } from '@/models/user';
import { isAxiosError } from 'axios';

interface AuthContextType {
    user?: UserResponse | null;
    isLoading: boolean;
    login: (email: string, password: string) => Promise<void>;
    register: (email: string, password: string, displayName?: string) => Promise<void>;
    logout: () => Promise<void>;
    refreshUser: () => Promise<void>;
    clearCurrentUser: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUser] = useState<UserResponse | null>(null);
    const [isLoading, setLoading] = useState(true);
    const refreshUser = useCallback(async () => {
        try {
            const userData = await authApi.getMe();
            setUser(userData);
        } catch (error) {
            if (isAxiosError(error) && error.response?.status === 401) {
                setUser(null);
                // Don't redirect here, let the ProtectedRoute handle it so we save the location
            }

            setUser(null);
        }
    }, []);

    useEffect(() => {
        refreshUser().finally(() => setLoading(false));
    }, [refreshUser]);

    const login = async (email: string, password: string) => {
        await authApi.login({ email, password });
        await refreshUser();
    };

    const register = async (email: string, password: string, displayName?: string) => {
        await authApi.register({ email, password, displayName });
        await refreshUser();
    };

    const logout = async () => {
        await authApi.logout();
        setUser(null);
    };

    const clearCurrentUser = () => {
        setUser(null);
    };

    return (
        <AuthContext.Provider value={{ user, isLoading, login, register, logout, refreshUser, clearCurrentUser }}>
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth() {
    const context = useContext(AuthContext);
    if (context === undefined) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
}
