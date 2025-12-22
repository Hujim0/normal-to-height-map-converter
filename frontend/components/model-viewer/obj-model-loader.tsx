'use client';

import { AutoFitCamera } from './auto-fit-camera';
import { LoadingIndicator } from './loading-indicator';
import { ErrorDisplay } from './error-display';
import { useOBJMTLLoader } from '@/hooks/use-objmtl-loader';

export const OBJModelLoader = ({
  objFile,
  mtlFile
}: {
  objFile: string;
  mtlFile?: string
}) => {
  const { model, loading, error } = useOBJMTLLoader(objFile, mtlFile);

  if (loading) return <LoadingIndicator />;
  if (error) return <ErrorDisplay message={error} />;
  if (!model) return null;

  return (
    <>
      <primitive object={model} scale={0.5} dispose={null} />
      <AutoFitCamera model={model} />
    </>
  );
};
