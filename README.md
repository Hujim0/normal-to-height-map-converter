Григорьев Давид Владимирович Р3315 389491

## Лабораторная работа №4

### usage:

```
docker compose up
```

### developing:

you should start those services
- nginx - for serving uploads
- nextjs - for serving frontend
- aspnet - backend for performing conversion

ngnix:
```
./dev-nginx.sh
```

frontend:
```
bun run dev
```

backend:
```
./dev.sh
```

### results:

![alt text](https://github.com/Hujim0/normal-to-height-map-converter/blob/main/examples/terrain/heightmap.png?raw=true)
![alt text](https://github.com/Hujim0/normal-to-height-map-converter/blob/main/examples/terrain/model.png?raw=true)

![alt text](https://github.com/Hujim0/normal-to-height-map-converter/blob/main/examples/test_normals/heightmap.png?raw=true)
![alt text](https://github.com/Hujim0/normal-to-height-map-converter/blob/main/examples/test_normals/model.png?raw=true)

based on research paper by ![Gavin D.J. Smith and Adrian G. Bors](https://www.researchgate.net/profile/Adrian-Bors/publication/3964099_Height_estimation_from_vector_fields_of_surface_normals/links/53eccccc0cf2981ada10eae5/Height-estimation-from-vector-fields-of-surface-normals.pdf?origin=publication_detail&_tp=eyJjb250ZXh0Ijp7ImZpcnN0UGFnZSI6Il9kaXJlY3QiLCJwYWdlIjoicHVibGljYXRpb25Eb3dubG9hZCIsInByZXZpb3VzUGFnZSI6Il9kaXJlY3QifX0)
