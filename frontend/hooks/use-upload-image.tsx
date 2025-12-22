// hooks/use-upload-image.ts
import { apiClient } from '@/lib/apiClient';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { toast } from 'sonner';

export interface UploadResponse {
    hash: string;
}

export interface ErrorResponse {
    error: string;
}

export function useUploadImage() {
    const queryClient = useQueryClient();

    return useMutation<UploadResponse, AxiosError<ErrorResponse>, File>({
        mutationFn: async (file) => {
            const formData = new FormData();
            formData.append('file', file);

            const response = await apiClient.post<UploadResponse>(
                '/api/upload-normal',
                formData,
                {
                    headers: {
                        'Content-Type': 'multipart/form-data',
                    },
                },
            );

            return response.data;
        },
        onSuccess: (data) => {
            toast.success('File uploaded successfully!', {
                description: `SHA-256: ${data.hash.substring(0, 8)}...`,
                duration: 5000,
            });
        },
        onError: (error) => {
            const errorMessage =
                error.response?.data?.error || 'Failed to upload image file';
            toast.error('Upload failed', {
                description: errorMessage,
            });
        },
    });
}
