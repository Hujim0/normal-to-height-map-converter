'use client';

import { Suspense, useRef, useState, useEffect } from 'react';
import { Canvas, useFrame, useThree } from '@react-three/fiber';
import { OrbitControls, Environment, Html, useProgress, useGLTF } from '@react-three/drei';
import { OBJLoader } from 'three/addons/loaders/OBJLoader.js';
import { MTLLoader } from 'three/addons/loaders/MTLLoader.js';
import * as THREE from 'three';
import { Loader2, AlertCircle, Box } from 'lucide-react';
import { Button } from "@/components/ui/button";

// Define proper types for controls
interface OrbitControlsType {
  target: THREE.Vector3;
  update: () => void;
}

// Custom type guard for OrbitControls
function isOrbitControls(controls: any): controls is OrbitControlsType {
  return controls && 'target' in controls && 'update' in controls;
}

// Custom OBJ+MTL loader hook with proper typing
const useOBJMTL = (objUrl: string, mtlUrl?: string) => {
  const [model, setModel] = useState<THREE.Group | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadModel = async () => {
      try {
        setLoading(true);
        let materials = null;

        if (mtlUrl) {
          const mtlLoader = new MTLLoader();
          materials = await mtlLoader.loadAsync(mtlUrl);
          materials.preload();
        }

        const objLoader = new OBJLoader();
        if (materials) objLoader.setMaterials(materials);

        const loadedModel = await objLoader.loadAsync(objUrl);
        loadedModel.traverse((child) => {
          if ((child as THREE.Mesh).isMesh) {
            const mesh = child as THREE.Mesh;

            if (mesh.material && !Array.isArray(mesh.material)) {
              mesh.material.side = THREE.DoubleSide;
              mesh.material.needsUpdate = true;
            }
            else if (Array.isArray(mesh.material)) {
              mesh.material.forEach((mat) => {
                if (mat) {
                  mat.side = THREE.DoubleSide;
                  mat.needsUpdate = true;
                }
              });
            }
          }
        });

        loadedModel.position.set(0, 0, 0);

        // Center the model
        const box = new THREE.Box3().setFromObject(loadedModel);
        const center = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3());

        const maxDim = Math.max(size.x, size.y, size.z);
        const fov = 50;
        const cameraZ = Math.abs(maxDim / Math.tan((fov * Math.PI) / 360));

        loadedModel.position.x = -center.x / 2;
        loadedModel.position.y = -center.y / 2;
        loadedModel.position.z = -center.z / 2;
        setModel(loadedModel);
        setError(null);
      } catch (err) {
        setError('Failed to load OBJ/MTL model');
        console.error('OBJ/MTL loading error:', err);
      } finally {
        setLoading(false);
      }
    };

    loadModel();
  }, [objUrl, mtlUrl]);

  return { model, loading, error };
};

// Loader component for showing loading state
const ModelLoader = () => {
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

// Error display component
const ModelError = ({ message }: { message: string }) => {
  return (
    <Html center>
      <div className="flex flex-col justify-center items-center bg-destructive/10 p-6 border border-destructive rounded-lg text-destructive">
        <AlertCircle className="mb-2 w-8 h-8" />
        <span className="font-medium text-center">{message}</span>
      </div>
    </Html>
  );
};

// GLB/GLTF Model Component
const GLBModel = ({ url }: { url: string }) => {
  const { scene } = useGLTF(url);
  const modelRef = useRef<THREE.Group>(null);

  useFrame(() => {
    if (modelRef.current) {
      modelRef.current.rotation.y += 0.01;
    }
  });

  return (
    <primitive
      ref={modelRef}
      object={scene}
      scale={0.5}
      dispose={null}
    />
  );
};


// Auto-fit camera to model bounds with proper typing
const AutoFitCamera = ({ model }: { model: THREE.Object3D }) => {
  const { camera, controls } = useThree();

  useEffect(() => {
    if (!model || !controls) return;

    const box = new THREE.Box3().setFromObject(model);
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());

    // Special handling for terrain models (flat, wide objects)
    const isTerrain = size.y < Math.min(size.x, size.z) * 0.1;

    // Cast camera to PerspectiveCamera
    if (!(camera instanceof THREE.PerspectiveCamera)) return;

    const fov = camera.fov * (Math.PI / 180);

    if (isTerrain) {
      // Terrain-specific camera positioning
      const diagonal = Math.sqrt(size.x * size.x + size.z * size.z);
      const cameraZ = Math.abs(diagonal / 2) / Math.tan(fov / 2);

      // Position camera above terrain looking down at 45 degrees
      const elevation = diagonal * 0.7; // Height above terrain
      const distance = cameraZ * 1.8;   // Extra padding for terrain

      camera.position.set(
        center.x,
        center.y + elevation,
        center.z + distance
      );

      // Look at a point slightly above the terrain center for better perspective
      camera.lookAt(new THREE.Vector3(
        center.x,
        center.y + size.y * 0.3, // Look slightly above ground level
        center.z
      ));
    } else {
      // Standard camera fitting for regular models
      const diagonal = Math.sqrt(size.x * size.x + size.y * size.y + size.z * size.z);
      const radius = diagonal / 2;
      const cameraZ = radius / Math.tan(fov / 2);

      camera.position.set(
        center.x,
        center.y,
        center.z + cameraZ * 1.8 // Increased padding
      );
      camera.lookAt(center);
    }

    // Update controls target
    if (isOrbitControls(controls)) {
      controls.target.copy(
        isTerrain
          ? new THREE.Vector3(center.x, center.y + size.y * 0.3, center.z)
          : center
      );
      controls.update();
    }
  }, [model, camera, controls]);

  return null;
};

// GLB Model Wrapper with AutoFitCamera
const GLBModelWithAutoFit = ({ url }: { url: string }) => {
  const { scene } = useGLTF(url);

  return (
    <>
      <GLBModel url={url} />
      <AutoFitCamera model={scene} />
    </>
  );
};

// OBJ Model Wrapper with AutoFitCamera
const OBJModelWithAutoFit = ({ objFile, mtlFile }: { objFile: string; mtlFile?: string }) => {
  const { model, loading, error } = useOBJMTL(objFile, mtlFile);

  if (loading) return <ModelLoader />;
  if (error) return <ModelError message={error} />;
  if (!model) return null;

  return (
    <>
      <primitive object={model} scale={0.5} dispose={null} />
      <AutoFitCamera model={model} />
    </>
  );
};

// Main Model Viewer Component
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

  const handleError = (event: MouseEvent) => {
    setError(`Failed to load model: Click missed`);
    console.error('Model loading error: pointer missed');
  };

  const handleRetry = () => {
    setError(null);
    // Force reload by adding timestamp to URL
    const timestamp = Date.now();
    window.location.href = `${window.location.href.split('?')[0]}?retry=${timestamp}`;
  };

  // Preload GLTF models
  useEffect(() => {
    if (modelType === 'glb' || modelType === 'gltf') {
      useGLTF.preload(modelUrl);
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
        // Increased initial distance for large terrains
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
          position={[500, 500, 200]} // Moved farther for large terrains
          intensity={1}
          castShadow
          shadow-mapSize={[2048, 2048]} // Higher resolution shadows
          shadow-camera-far={1000}
        />
        <hemisphereLight intensity={0.3} groundColor="lightblue" />

        <Suspense fallback={<ModelLoader />}>
          {modelType === 'glb' || modelType === 'gltf' ? (
            <GLBModelWithAutoFit url={modelUrl} />
          ) : modelType === 'obj' ? (
            <OBJModelWithAutoFit objFile={modelUrl} mtlFile={mtlUrl} />
          ) : null}
        </Suspense>

        <Environment preset="city" />
        <OrbitControls
          enableDamping
          dampingFactor={0.1}
          rotateSpeed={0.5}
          minDistance={50}    // Increased min distance for large terrains
          maxDistance={1000}  // Increased max distance
          autoRotate={false}
          // Prevent extreme vertical rotations for terrains
          maxPolarAngle={Math.PI * 0.45} // About 80 degrees from vertical
        />
      </Canvas>
    </div>
  );
};

// Helper component for model info panel
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
