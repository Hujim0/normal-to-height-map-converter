'use client';

import { Box } from 'lucide-react';

export const ModelInfoPanel = ({
  filename,
  size,
  format
}: {
  filename: string;
  size: number;
  format: string;
}) => {
  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  return (
    <div className="bg-muted/50 p-3 border rounded-lg">
      <div className="flex items-center gap-2 mb-1">
        <Box className="w-4 h-4 text-purple-500" />
        <span className="font-medium truncate">{filename}</span>
      </div>
      <div className="space-y-1 text-muted-foreground text-xs">
        <p>Format: <span className="font-medium">{format.toUpperCase()}</span></p>
        <p>Size: <span className="font-medium">{formatFileSize(size)}</span></p>
        <p className="text-blue-500">Interactive 3D Preview</p>
      </div>
    </div>
  );
};
