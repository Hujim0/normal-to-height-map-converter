'use client';

import { Html } from '@react-three/drei';
import { AlertCircle } from 'lucide-react';

export const ErrorDisplay = ({ message }: { message: string }) => {
  return (
    <Html center>
      <div className="flex flex-col justify-center items-center bg-destructive/10 p-6 border border-destructive rounded-lg text-destructive">
        <AlertCircle className="mb-2 w-8 h-8" />
        <span className="font-medium text-center">{message}</span>
      </div>
    </Html>
  );
};
