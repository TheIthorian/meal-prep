import { ChangeEvent, useState } from 'react';
import { MAX_FILE_UPLOAD_SIZE, MAX_FILE_UPLOAD_SIZE_MB } from '../constants';

export function useFiles() {
    const [error, setError] = useState<string>();
    const [filesState, setFilesState] = useState<FileList>();
    const [files, setFiles] = useState<File[]>();

    function onUpload(e: ChangeEvent<HTMLInputElement>) {
        if (!e.target.files) return;

        const fls = e.target?.files;
        const allFiles = [];

        let totalSize = 0;
        for (const file of fls) {
            totalSize += file.size;
            allFiles.push(file);
        }

        if (totalSize > MAX_FILE_UPLOAD_SIZE) {
            setError(`File too large! The max file size is ${MAX_FILE_UPLOAD_SIZE_MB}mb`);
            setFilesState(undefined);
            return;
        }

        setFilesState(fls);
        setFiles(allFiles);
    }

    function clearInput() {
        setFilesState(undefined);
        setFiles([]);
        setError('');
    }

    return { error, filesState, onUpload, hasFile: !!filesState, files, clearInput };
}
