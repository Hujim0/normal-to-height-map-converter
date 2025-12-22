'use client';

import { Html } from '@react-three/drei';
import { Loader2 } from 'lucide-react';
import { useProgress } from '@react-three/drei';

export const LoadingIndicator = () => {
  const { progress } = useProgress();
  return (
    <Html center>
      <div className="flex flex-col justify-center items-center bg-background/80 backdrop-blur-sm p-6 border rounded-lg">
        <Loader2 className="mb-4 w-8 h-8 text-primary animate-spin" />
        <span className="font-medium text-sm">Loading model: {Math.round(progress)}%</span>
      </div>
    </Html>
  );
};
