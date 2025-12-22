"use client"

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { AlertCircle, CheckCircle, ImageIcon, Loader2, X } from "lucide-react";
import React from "react";
import { toast } from "sonner";
import { useUploadImage } from "@/hooks/use-upload-image";
import { useRouter } from "next/navigation";

export default function Home() {
  const navigation = useRouter();
  const [file, setFile] = React.useState<File | null>(null);
  const [dragActive, setDragActive] = React.useState(false);
  const [uploadHash, setUploadHash] = React.useState<string | null>(null);
  const { mutate: uploadImage, isPending, error } = useUploadImage();

  const handleFileDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);

    const droppedFile = e.dataTransfer.files[0];
    if (droppedFile) {
      validateAndSetFile(droppedFile);
    }
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (selectedFile) {
      validateAndSetFile(selectedFile);
    }
    e.target.value = "";
  };

  const validateAndSetFile = (file: File) => {
    // Validate file type
    const validTypes = [
      'image/jpeg',
      'image/png',
      'image/gif',
      'image/webp',
      'image/bmp',
      'image/tiff'
    ];
    const fileExtension = file.name.split('.').pop()?.toLowerCase();
    const isValidType = validTypes.includes(file.type) ||
      (fileExtension && validTypes.some(type => type.includes(fileExtension)));

    if (!isValidType) {
      toast.error("Invalid file type. Please upload an image (JPEG, PNG, GIF, WebP, BMP, or TIFF).");
      return;
    }

    // Validate file size (limit to 10MB)
    if (file.size > 10 * 1024 * 1024) {
      toast.error("File size exceeds 10MB limit.");
      return;
    }

    setFile(file);
    setUploadHash(null);
  };

  const handleUpload = () => {
    if (!file) {
      toast.error("Please select an image to upload.");
      return;
    }

    uploadImage(file, {
      onSuccess: (data) => {
        setUploadHash(data.hash);
        navigation.push(`/results?hash=${data.hash}`)
      }
    });
  };

  const handleReset = () => {
    setFile(null);
    setUploadHash(null);
  };

  return (
    <div className="flex p-4 md:p-8 w-full min-h-screen">
      <div className="space-y-6 mx-auto w-full max-w-3xl">
        <div className="flex justify-between items-center">
          <h1 className="font-bold text-2xl">Image Uploader</h1>
          <div className="flex items-center gap-2">
            {/* Mode toggle can be added here if needed */}
          </div>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Upload Image</CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* File Upload Area */}
            {!uploadHash && (
              <div
                className={`border-2 border-dashed rounded-lg p-8 text-center transition-all duration-200 ${dragActive
                  ? "border-primary bg-primary/5"
                  : file
                    ? "border-green-500 "
                    : "border-border hover:border-primary/50"
                  }`}
                onDragOver={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  setDragActive(true);
                }}
                onDragLeave={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  setDragActive(false);
                }}
                onDrop={handleFileDrop}
              >
                <input
                  type="file"
                  id="upload-file"
                  className="hidden"
                  accept="image/jpeg,image/png,image/gif,image/webp,image/bmp,image/tiff"
                  onChange={handleFileInputChange}
                />
                <Label
                  htmlFor="upload-file"
                  className="flex flex-col justify-center items-center gap-4 cursor-pointer"
                >
                  {file ? (
                    <>
                      <div className="p-3 rounded-full">
                        <CheckCircle className="w-10 h-10 text-green-600" />
                      </div>
                      <div>
                        <p className="font-medium text-green-700">{file.name}</p>
                        <p className="text-muted-foreground">
                          {(file.size / 1024).toFixed(1)} KB
                        </p>
                      </div>
                      <Button variant="outline" size="sm" onClick={handleReset}>
                        <X className="mr-2 w-4 h-4" />
                        Change file
                      </Button>
                    </>
                  ) : (
                    <>
                      <div className="p-4 rounded-full">
                        <ImageIcon className="w-12 h-12 text-primary" />
                      </div>
                      <div>
                        <p className="font-medium text-lg">Drag & drop your image here</p>
                        <p className="mt-1 text-muted-foreground">
                          or click to browse (Max 10MB)
                        </p>
                        <p className="mt-2 text-muted-foreground text-xs">
                          Supported formats: JPG, PNG, GIF, WebP, BMP, TIFF
                        </p>
                      </div>
                    </>
                  )}
                </Label>
              </div>
            )}

            {/* Upload Result */}
            {uploadHash && (
              <Card className="border-green-500">
                <CardHeader>
                  <CardTitle className="flex items-center gap-2 text-green-600">
                    <CheckCircle className="w-5 h-5" />
                    Upload Successful
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-4">
                    <div>
                      <p className="mb-2 font-medium">File Hash (SHA-256):</p>
                      <div className="bg-muted p-3 rounded-md font-mono text-sm break-all">
                        {uploadHash}
                      </div>
                    </div>
                    <Button onClick={handleReset} className="w-full">
                      Upload Another File
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Upload Button */}
            {!uploadHash && (
              <div className="flex justify-center pt-4">
                <Button
                  size="lg"
                  onClick={handleUpload}
                  disabled={isPending || !file}
                  className="min-w-[200px]"
                >
                  {isPending ? (
                    <>
                      <Loader2 className="mr-2 w-4 h-4 animate-spin" />
                      Uploading...
                    </>
                  ) : (
                    "Upload Image"
                  )}
                </Button>
              </div>
            )}

            {/* Error Display */}
            {error && !isPending && !uploadHash && (
              <div className="bg-destructive/10 p-4 border border-destructive rounded-lg">
                <div className="flex items-center gap-2 text-destructive">
                  <AlertCircle className="flex-shrink-0 w-5 h-5" />
                  <p>{error.response?.data?.error || "Upload failed. Please try again."}</p>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
