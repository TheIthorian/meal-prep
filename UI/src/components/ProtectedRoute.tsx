import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '@/contexts/AuthContext';
import { FullPageSpinner } from '@/components/FullPageSpinner';

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
    const { user, isLoading } = useAuth();
    const location = useLocation();

    if (isLoading) {
        return <FullPageSpinner />;
    }

    if (!user) {
        return <Navigate to='/login' state={{ from: location }} replace />;
    }

    return <>{children}</>;
}
