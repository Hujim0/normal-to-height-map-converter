'use client';

import { useEffect, useState } from 'react';
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Download, Loader2, AlertCircle, FileText, LucideImage, Box } from "lucide-react";
import { toast } from "sonner";
import { ModelViewer } from '@/components/model-viewer/model-viewer';
import { ModelInfoPanel } from '@/components/model-viewer/model-info-panel';

interface FileItem {
  filename: string;
  size: number;
  last_modified: string;
  type: 'image' | 'model' | 'metadata';
  url: string;
  preview_url?: string;
}

export function ResultsClient() {
  const [hash, setHash] = useState<string | null>(null);
  const [files, setFiles] = useState<FileItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const urlParams = new URLSearchParams(window.location.search);
      const hashParam = urlParams.get('hash');
      setHash(hashParam);
    }
  }, []);

  useEffect(() => {
    if (!hash) {
      setLoading(false);
      return;
    }

    const fetchFiles = async () => {
      try {
        setLoading(true);
        const response = await fetch(`/api/upload-list/${hash}`);

        if (!response.ok) {
          const errorData = await response.json();
          throw new Error(errorData.error || 'Failed to fetch files');
        }

        const data = await response.json();
        setFiles(data);
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'An error occurred';
        setError(errorMessage);
        toast.error('Failed to load files', {
          description: errorMessage,
        });
      } finally {
        setLoading(false);
      }
    };

    fetchFiles();
  }, [hash]);

  const handleDownload = (url: string, filename: string) => {
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const getFileIcon = (type: string, filename: string) => {
    const extension = filename.split('.').pop()?.toLowerCase();

    if (type === 'image' || ['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'tiff'].includes(extension || '')) {
      return <LucideImage className="w-5 h-5 text-blue-500" />;
    }

    if (type === 'model' || ['obj', 'mtl', 'glb', 'gltf'].includes(extension || '')) {
      return <Box className="w-5 h-5 text-purple-500" />;
    }

    return <FileText className="w-5 h-5 text-gray-500" />;
  };

  const findMtlFile = (objFilename: string) => {
    const baseName = objFilename.replace(/\.obj$/i, '');
    return files.find(file =>
      file.filename.toLowerCase() === `${baseName}.mtl`.toLowerCase()
    );
  };

  if (loading && hash === null) {
    return (
      <div className="flex justify-center items-center min-h-screen">
        <Loader2 className="w-8 h-8 text-muted-foreground animate-spin" />
      </div>
    );
  }

  if (!hash) {
    return (
      <div className="flex justify-center items-center min-h-screen">
        <Alert variant="destructive">
          <AlertCircle className="w-4 h-4" />
          <AlertTitle>Invalid URL</AlertTitle>
          <AlertDescription>No hash parameter found in URL. Please upload a file first.</AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="w-full">
      <div className="space-y-6">
        <div className="flex justify-between items-center">
          <h1 className="font-bold text-2xl">Processing Results</h1>
          <Button onClick={() => window.history.back()} variant="outline">
            Upload Another File
          </Button>
        </div>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <span>Files for hash:</span>
              <span className="bg-muted px-2 py-1 rounded font-mono text-muted-foreground text-sm">
                {hash.substring(0, 8)}...{hash.substring(hash.length - 8)}
              </span>
            </CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="flex justify-center items-center h-48">
                <Loader2 className="w-8 h-8 text-muted-foreground animate-spin" />
              </div>
            ) : error ? (
              <Alert variant="destructive">
                <AlertCircle className="w-4 h-4" />
                <AlertTitle>Error</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            ) : files.length === 0 ? (
              <Alert>
                <AlertCircle className="w-4 h-4" />
                <AlertTitle>No files found</AlertTitle>
                <AlertDescription>No files were generated for this hash. Please try uploading again.</AlertDescription>
              </Alert>
            ) : (
              <div className="space-y-6">
                {/* Preview Images Section */}
                <div className="gap-4 grid grid-cols-1 md:grid-cols-2">
                  {files
                    .filter(file => file.type === 'image')
                    .map((file) => (
                      <Card key={file.filename} className="overflow-hidden">
                        <CardContent className="p-4">
                          <div className="relative bg-muted mb-4 rounded-lg aspect-square overflow-hidden">
                            <img
                              src={file.url}
                              alt={file.filename}
                              className="w-full h-full object-contain"
                              onError={(e) => {
                                const target = e.target as HTMLImageElement;
                                target.onerror = null;
                                target.parentElement!.innerHTML = '<div class="flex justify-center items-center h-full text-muted-foreground">Preview not available</div>';
                              }}
                            />
                          </div>
                          <div className="flex justify-between items-center">
                            <div>
                              <p className="flex items-center gap-2 font-medium">
                                {getFileIcon(file.type, file.filename)}
                                {file.filename}
                              </p>
                              <p className="text-muted-foreground text-sm">
                                {formatFileSize(file.size)}
                              </p>
                            </div>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => handleDownload(file.url, file.filename)}
                            >
                              <Download className="mr-2 w-4 h-4" />
                              Download
                            </Button>
                          </div>
                        </CardContent>
                      </Card>
                    ))}
                </div>

                {/* 3D Models and Other Files Section */}
                {files.some(file => file.type !== 'image') && (
                  <div>
                    <h2 className="mb-4 font-semibold text-lg">3D Models & Files</h2>
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>File</TableHead>
                          <TableHead>Size</TableHead>
                          <TableHead>Last Modified</TableHead>
                          <TableHead>Actions</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {files
                          .filter(file => file.type !== 'image')
                          .map((file) => (
                            <TableRow key={file.filename}>
                              <TableCell className="flex items-center gap-2 font-medium">
                                {getFileIcon(file.type, file.filename)}
                                {file.filename}
                              </TableCell>
                              <TableCell>{formatFileSize(file.size)}</TableCell>
                              <TableCell>
                                {new Date(file.last_modified).toLocaleString()}
                              </TableCell>
                              <TableCell>
                                <div className="flex gap-2">
                                  <Button
                                    size="sm"
                                    variant="outline"
                                    onClick={() => handleDownload(file.url, file.filename)}
                                  >
                                    <Download className="mr-1 w-4 h-4" />
                                    Download
                                  </Button>
                                </div>
                              </TableCell>
                            </TableRow>
                          ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </div>
            )}
          </CardContent>
        </Card>

        {/* Instructions Section - Only show for actual 3D models (not MTL files) */}
        {files.some(file => {
          const ext = file.filename.split('.').pop()?.toLowerCase();
          return ext === 'obj' || ext === 'glb' || ext === 'gltf';
        }) && (
            <div className="space-y-6">
              {files
                .filter(file => {
                  const ext = file.filename.split('.').pop()?.toLowerCase();
                  return ext === 'obj' || ext === 'glb' || ext === 'gltf';
                })
                .map((file) => {
                  const format = file.filename.split('.').pop()?.toLowerCase() || '';
                  const isGLB = ['glb', 'gltf'].includes(format);
                  const isOBJ = format === 'obj';
                  const mtlFile = isOBJ ? findMtlFile(file.filename) : null;

                  return (
                    <div key={file.filename} className="space-y-4">
                      <div className="flex md:flex-row flex-col gap-4">
                        <div className="w-full md:w-2/3">
                          <ModelViewer
                            modelUrl={file.url}
                            modelType={format as 'glb' | 'gltf' | 'obj'}
                            mtlUrl={mtlFile?.url}
                          />
                        </div>
                        <div className="w-full md:w-1/3">
                          <ModelInfoPanel
                            filename={file.filename}
                            size={file.size}
                            format={format}
                          />
                          <div className="flex flex-wrap gap-2 mt-4">
                            <Button
                              className="w-full"
                              onClick={() => handleDownload(file.url, file.filename)}
                            >
                              <Download className="mr-2 w-4 h-4" />
                              Download Model
                            </Button>
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })}
            </div>
          )}
      </div>
    </div>
  );
}
