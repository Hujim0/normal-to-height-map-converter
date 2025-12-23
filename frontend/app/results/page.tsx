// app/results/page.tsx
import { Suspense } from 'react';
import { ResultsClient } from './results-client';
import { Skeleton } from '@/components/ui/skeleton';

export default function ResultsPage() {
  return (
    <div className="flex p-4 md:p-8 w-full min-h-screen">
      <div className="space-y-6 mx-auto w-full max-w-4xl">
        <Suspense fallback={
          <div className="flex justify-center items-center min-h-screen">
            <Skeleton className="h-12 w-64" />
          </div>
        }>
          <ResultsClient />
        </Suspense>
      </div>
    </div>
  );
}
