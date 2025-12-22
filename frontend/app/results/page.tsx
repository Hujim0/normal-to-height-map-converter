// src/[hash]/page.tsx
'use client';

import { useParams, useSearchParams } from 'next/navigation';
import { useEffect, useState } from 'react';
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Download, Loader2, AlertCircle, FileText, LucideImage, Box } from "lucide-react";
import { toast } from "sonner";
import Image from 'next/image';

interface FileItem {
    filename: string;
    size: number;
    last_modified: string;
    type: 'image' | 'model' | 'metadata';
    url: string;
    preview_url?: string;
}

export default function ResultsPage() {
    const searchParams = useSearchParams();
    const hash = searchParams.get('hash');
    const [files, setFiles] = useState<FileItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!hash) return;

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

    if (!hash) {
        return (
            <div className="flex justify-center items-center min-h-screen">
                <Alert variant="destructive">
                    <AlertCircle className="w-4 h-4" />
                    <AlertTitle>Invalid URL</AlertTitle>
                    <AlertDescription>No hash parameter found in URL</AlertDescription>
                </Alert>
            </div>
        );
    }

    return (
        <div className="flex p-4 md:p-8 w-full min-h-screen">
            <div className="space-y-6 mx-auto w-full max-w-4xl">
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
                                                                    {file.preview_url && (
                                                                        <Button
                                                                            size="sm"
                                                                            variant="secondary"
                                                                            onClick={() => window.open(file.preview_url, '_blank')}
                                                                        >
                                                                            Preview
                                                                        </Button>
                                                                    )}
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

                {/* Instructions Section */}
                <Card>
                    <CardHeader>
                        <CardTitle>How to Use These Files</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div>
                            <h3 className="flex items-center gap-2 mb-2 font-medium">
                                <LucideImage className="w-4 h-4 text-blue-500" />
                                Image Files (PNG, JPG, etc.)
                            </h3>
                            <p className="text-muted-foreground text-sm">
                                These can be used directly in HTML img tags or as textures in 3D applications.
                            </p>
                        </div>
                        <div>
                            <h3 className="flex items-center gap-2 mb-2 font-medium">
                                <Box className="w-4 h-4 text-purple-500" />
                                3D Model Files (.obj, .mtl)
                            </h3>
                            <p className="text-muted-foreground text-sm">
                                These can be loaded into three.js or other 3D frameworks. Use the .obj file for geometry and .mtl for materials.
                            </p>
                        </div>
                        <div className="bg-muted p-3 rounded-md">
                            <p className="mb-2 font-medium text-sm">Example three.js usage:</p>
                            <pre className="bg-background p-2 border rounded overflow-x-auto text-xs">
                                {`import * as THREE from 'three';
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader';
import { MTLLoader } from 'three/examples/jsm/loaders/MTLLoader';

// Load materials first
const mtlLoader = new MTLLoader();
mtlLoader.load('/uploads/${hash}/model.mtl', (materials) => {
  materials.preload();

  // Then load the model
  const objLoader = new OBJLoader();
  objLoader.setMaterials(materials);
  objLoader.load('/uploads/${hash}/model.obj', (object) => {
    scene.add(object);
  });
});`}
                            </pre>
                        </div>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
