'use client';

import { useEffect } from 'react';
import { useThree } from '@react-three/fiber';
import * as THREE from 'three';

interface OrbitControlsType {
  target: THREE.Vector3;
  update: () => void;
}

export function isOrbitControls(controls: any): controls is OrbitControlsType {
  return controls && 'target' in controls && 'update' in controls;
}

export const AutoFitCamera = ({ model }: { model: THREE.Object3D }) => {
  const { camera, controls } = useThree();

  useEffect(() => {
    if (!model || !controls) return;

    const box = new THREE.Box3().setFromObject(model);
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());

    const isTerrain = size.y < Math.min(size.x, size.z) * 0.1;

    if (!(camera instanceof THREE.PerspectiveCamera)) return;

    const fov = camera.fov * (Math.PI / 180);

    if (isTerrain) {
      const diagonal = Math.sqrt(size.x * size.x + size.z * size.z);
      const cameraZ = Math.abs(diagonal / 2) / Math.tan(fov / 2);

      const elevation = diagonal * 0.7;
      const distance = cameraZ * 1.8;

      camera.position.set(
        center.x,
        center.y + elevation,
        center.z + distance
      );

      camera.lookAt(new THREE.Vector3(
        center.x,
        center.y + size.y * 0.3,
        center.z
      ));
    } else {
      const diagonal = Math.sqrt(size.x * size.x + size.y * size.y + size.z * size.z);
      const radius = diagonal / 2;
      const cameraZ = radius / Math.tan(fov / 2);

      camera.position.set(
        center.x,
        center.y,
        center.z + cameraZ * 1.8
      );
      camera.lookAt(center);
    }

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
