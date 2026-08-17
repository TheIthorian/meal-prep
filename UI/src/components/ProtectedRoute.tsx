import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { FullPageSpinner } from '@/components/FullPageSpinner';
import { buildAuthPath } from '@/lib/return-url';

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
    const { user, isLoading } = useAuth();
    const location = useLocation();

    if (isLoading) {
        return <FullPageSpinner />;
    }

    if (!user) {
        const loginPath = buildAuthPath('/login', `${location.pathname}${location.search}${location.hash}`);
        return <Navigate to={loginPath} state={{ from: location }} replace />;
    }

    return <>{children}</>;
}
