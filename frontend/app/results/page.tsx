// app/results/page.tsx
import { ResultsClient } from './results-client';

export default function ResultsPage() {
  return (
    <div className="flex p-4 md:p-8 w-full min-h-screen">
      <div className="space-y-6 mx-auto w-full max-w-4xl">
        <ResultsClient />
      </div>
    </div>
  );
}
