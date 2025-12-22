export UPLOAD_PATH=./../uploads

rm -rf ${UPLOAD_PATH}
mkdir -p ${UPLOAD_PATH}

dotnet run --project NormalToHeightMapConverter.Web
