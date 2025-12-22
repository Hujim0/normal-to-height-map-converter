"use client";

import { useState, useEffect } from "react";
import { OBJLoader } from "three/addons/loaders/OBJLoader.js";
import { MTLLoader } from "three/addons/loaders/MTLLoader.js";
import * as THREE from "three";

export const useOBJMTLLoader = (objUrl: string, mtlUrl?: string) => {
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
                        } else if (Array.isArray(mesh.material)) {
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
                const cameraZ = Math.abs(
                    maxDim / Math.tan((fov * Math.PI) / 360)
                );

                loadedModel.position.x = -center.x / 2;
                loadedModel.position.y = -center.y / 2;
                loadedModel.position.z = -center.z / 2;
                setModel(loadedModel);
                setError(null);
            } catch (err) {
                setError("Failed to load OBJ/MTL model");
                console.error("OBJ/MTL loading error:", err);
            } finally {
                setLoading(false);
            }
        };

        loadModel();
    }, [objUrl, mtlUrl]);

    return { model, loading, error };
};
