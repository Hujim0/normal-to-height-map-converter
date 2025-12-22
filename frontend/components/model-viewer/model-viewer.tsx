'use client';

import { Suspense, useState, useEffect } from 'react';
import { Canvas } from '@react-three/fiber';
import { OrbitControls, Environment, useGLTF } from '@react-three/drei';
import * as THREE from 'three';
import { AlertCircle } from 'lucide-react';
import { Button } from "@/components/ui/button";
import { LoadingIndicator } from './loading-indicator';
import { OBJModelLoader } from './obj-model-loader';
import { GLBModel } from './glb-model';
import { AutoFitCamera } from './auto-fit-camera';

export const ModelViewer = ({
  modelUrl,
  modelType,
  mtlUrl
}: {
  modelUrl: string;
  modelType: 'glb' | 'gltf' | 'obj';
  mtlUrl?: string;
}) => {
  const [error, setError] = useState<string | null>(null);

  const handleError = () => {
    setError(`Failed to load model: Click missed`);
    console.error('Model loading error: pointer missed');
  };

  const handleRetry = () => {
    setError(null);
    window.location.reload();
  };

  useEffect(() => {
    if (modelType === 'glb' || modelType === 'gltf') {
      import('@react-three/drei').then((drei) => {
        drei.useGLTF.preload(modelUrl);
      });
    }
  }, [modelType, modelUrl]);

  if (error) {
    return (
      <div className="flex flex-col justify-center items-center bg-muted p-4 border border-dashed rounded-lg w-full h-64 md:h-96">
        <AlertCircle className="mb-2 w-8 h-8 text-destructive" />
        <p className="mb-4 text-muted-foreground text-sm text-center">{error}</p>
        <Button variant="outline" size="sm" onClick={handleRetry}>
          Retry Loading
        </Button>
      </div>
    );
  }

  return (
    <div className="bg-background border rounded-lg w-full h-64 md:h-96 overflow-hidden">
      <Canvas
        camera={{ position: [0, 150, 300], fov: 50 }}
        onCreated={({ gl, camera }) => {
          gl.setClearColor(0x1e1e2e);
          gl.toneMapping = THREE.ACESFilmicToneMapping;
          gl.outputColorSpace = THREE.SRGBColorSpace;

          if (camera instanceof THREE.PerspectiveCamera) {
            camera.far = 5000;
          }
        }}
        onPointerMissed={handleError}
        dpr={[1, 2]}
      >
        <color attach="background" args={[0x1e1e2e]} />
        <ambientLight intensity={0.5} />
        <directionalLight
          position={[500, 500, 200]}
          intensity={1}
          castShadow
          shadow-mapSize={[2048, 2048]}
          shadow-camera-far={1000}
        />
        <hemisphereLight intensity={0.3} groundColor="lightblue" />

        <Suspense fallback={<LoadingIndicator />}>
          {modelType === 'glb' || modelType === 'gltf' ? (
            <GLBModelWithAutoFit url={modelUrl} />
          ) : modelType === 'obj' ? (
            <OBJModelLoader objFile={modelUrl} mtlFile={mtlUrl} />
          ) : null}
        </Suspense>

        <Environment preset="city" />
        <OrbitControls
          enableDamping
          dampingFactor={0.1}
          rotateSpeed={0.5}
          minDistance={50}
          maxDistance={1000}
          autoRotate={false}
          maxPolarAngle={Math.PI * 0.45}
        />
      </Canvas>
    </div>
  );
};

const GLBModelWithAutoFit = ({ url }: { url: string }) => {
  const { scene } = useGLTF(url);
  return (
    <>
      <GLBModel url={url} />
      <AutoFitCamera model={scene} />
    </>
  );
};
