// src/lib/apiClient.ts
import axios from 'axios';

export const apiClient = axios.create({
  timeout: 10000,
  baseURL: 'api/',
});

// Add interceptors here if needed
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // Global error handling
    if (error.response?.status === 401) {
      // Handle unauthorized
    }
    return Promise.reject(error);
  },
);
