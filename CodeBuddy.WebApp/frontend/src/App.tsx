import AceEditor from "react-ace";
import './App.css'

import "ace-builds/src-noconflict/mode-java";
import "ace-builds/src-noconflict/theme-github";
import "ace-builds/src-noconflict/ext-language_tools";
import {ChangeEvent, useEffect, useRef, useState} from "react";
import {Ace} from "ace-builds";
import {CodeGram, EventType} from "./types.ts";
import {Alert, Button, Flex, Input, Layout} from "antd";
import {Content} from "antd/lib/layout/layout";
import {Header} from "antd/es/layout/layout";


function App() {
    const [editor, setEditor] = useState<Ace.Editor>();
    const [transport, setTransport] = useState<WebTransport>();
    const [stream, setStream] = useState<WebTransportBidirectionalStream>();
    const [encoder] = useState(new TextEncoder());
    const [decoder] = useState(new TextDecoder());
    const [inputStatus, setInputStatus] = useState<"" | "error">("");
    const [readOnly, setReadOnly] = useState(true)
    const [alertMessage, setAlertMessage] = useState<string>()
    const [alertType, setAlertType] = useState<"success" | "error">();
    const boardIdentifier = useRef<string>();
    const userId = useRef<string>();
    const writer = useRef<WritableStreamDefaultWriter>();

    async function retrieveCertificateHash(): Promise<string> {
        const response = await fetch('http://localhost:5001/api/certs/hash')
        return response.text()
    }

    async function setupWebTransport() {
        const url = 'https://localhost:4433';
        const certHash = await retrieveCertificateHash();
        const transport = new WebTransport(url, {
            serverCertificateHashes: [{
                algorithm: "sha-256",
                value: Uint8Array.from(atob(certHash), c => c.charCodeAt(0)),
            }]
        });
        await transport.ready;
        setTransport(transport);
        const stream = await transport.createBidirectionalStream();
        setStream(stream);
        if (!writer.current) {
            writer.current = stream!.writable.getWriter();
        }
    }

    useEffect(() => {
        setupWebTransport();
        return () => {
            transport?.close()
        }
    }, []);

    useEffect(() => {
        if (editor && stream) {
            handleIncomingReads()
            return () => {
                unsubscribeFromBoard();
            }
        }
    }, [editor, stream]);

    async function handleIncomingReads() {
        const reader = stream!.readable.getReader()
        let readDone = false
        userId.current = "";
        while (!readDone) {
            const {value} = await reader.read();
            const rawJson = decoder.decode(value);
            const codeGram = JSON.parse(rawJson) as CodeGram
            if (codeGram.type === EventType.Connect) {
                if (!userId.current) {
                    userId.current = codeGram.userId
                }
                if (!boardIdentifier.current) {
                    boardIdentifier.current = codeGram.board;
                }
                console.log("Connected to board: " + boardIdentifier.current);
                console.log("Connected as user: " + userId.current);
                setAlertType("success")
                setAlertMessage("Connected Successfully, please share the board id above for collaboration")
                setReadOnly(false)
            }
            if (codeGram.data && codeGram.type == EventType.Message && codeGram.userId !== userId.current) {
                const delta = JSON.parse(codeGram.data);
                editor!.getSession().getDocument().applyDelta({...delta, userId: codeGram.userId})
            }
        }
    }

    async function subscribeToBoard() {
        await writer.current?.write(encoder.encode(JSON.stringify({
            board: boardIdentifier.current,
            type: EventType.Connect,
            data: ""
        } as CodeGram)))
    }

    async function unsubscribeFromBoard() {
        console.log(boardIdentifier.current)
        console.log(userId.current)
        if (boardIdentifier.current) {
            await writer.current?.write(encoder.encode(JSON.stringify({
                board: boardIdentifier.current,
                userId: userId.current,
                type: EventType.Disconnect,
            } as CodeGram)));
            userId.current = ""
            boardIdentifier.current = ""
            setReadOnly(true)
            setAlertMessage("")
        }
    }

    async function handleChange(_: string, diff: any) {
        if (!diff.userId && writer.current) {
            await writer.current.write(encoder.encode(JSON.stringify({
                board: boardIdentifier.current,
                userId: userId.current,
                type: EventType.Message,
                data: JSON.stringify(diff)
            } as CodeGram)));
        }
    }

    async function handleJoin() {
        if (!boardIdentifier.current) {
            setInputStatus("error")
            setAlertType("error")
            setAlertMessage("Please enter the board id you wish to join!")
        } else {
            setInputStatus("")
            await subscribeToBoard()
        }
    }

    function handleBoardIdentifierChange(evt: ChangeEvent<HTMLInputElement>) {
        boardIdentifier.current = evt.target.value
    }

    return (
        <>
            <Layout>
                <Header style={{paddingTop: "16px"}}>
                    <Flex gap="small">
                        <Input status={inputStatus} onChange={handleBoardIdentifierChange}
                               value={boardIdentifier.current}
                               readOnly={userId.current !== undefined}></Input>
                        {
                            userId.current ? <Button type="primary" onClick={unsubscribeFromBoard}>Close</Button> :
                                (<>
                                    <Button type="primary" onClick={handleJoin}>Join</Button>
                                    <Button type="dashed" onClick={subscribeToBoard}>Create</Button>
                                </>)
                        }
                    </Flex>
                </Header>
                <Content>
                    {alertMessage && (<Alert message={alertMessage} type={alertType} closable/>)}
                    <AceEditor
                        mode="java"
                        theme="github"
                        readOnly={readOnly}
                        minLines={100}
                        width={screen.width.toString()}
                        onLoad={setEditor}
                        onChange={handleChange}
                    />
                </Content>
            </Layout>
        </>
    )
}

export default App
