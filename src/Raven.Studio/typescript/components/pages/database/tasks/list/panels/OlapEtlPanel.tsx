﻿import React from "react";
import {
    BaseOngoingTaskPanelProps,
    ConnectionStringItem,
    OngoingTaskActions,
    OngoingTaskName,
    OngoingTaskStatus,
    useTasksOperations,
} from "../shared";
import { OngoingTaskOlapEtlInfo } from "../../../../../models/tasks";
import { useAccessManager } from "hooks/useAccessManager";
import { useAppUrls } from "hooks/useAppUrls";
import { RichPanel, RichPanelDetailItem, RichPanelDetails, RichPanelHeader } from "../../../../../common/RichPanel";

type OlapEtlPanelProps = BaseOngoingTaskPanelProps<OngoingTaskOlapEtlInfo>;

function Details(props: OlapEtlPanelProps & { canEdit: boolean }) {
    const { data, canEdit, db } = props;
    const { appUrl } = useAppUrls();
    const connectionStringsUrl = appUrl.forConnectionStrings(db, "olap", data.shared.connectionStringName);
    //TODO: task status
    //TODO: progress
    return (
        <RichPanelDetails>
            {data.shared.destinations.map((dst) => (
                <RichPanelDetailItem key={dst}>
                    Destination:
                    <div className="value">{dst}</div>
                </RichPanelDetailItem>
            ))}
            <ConnectionStringItem
                connectionStringDefined
                canEdit={canEdit}
                connectionStringName={data.shared.connectionStringName}
                connectionStringsUrl={connectionStringsUrl}
            />
        </RichPanelDetails>
    );
}

export function OlapEtlPanel(props: OlapEtlPanelProps) {
    const { db, data } = props;

    const { isAdminAccessOrAbove } = useAccessManager();
    const { forCurrentDatabase } = useAppUrls();

    const canEdit = isAdminAccessOrAbove(db) && !data.shared.serverWide;
    const editUrl = forCurrentDatabase.editOlapEtl(data.shared.taskId)();

    const { detailsVisible, toggleDetails, toggleStateHandler, onEdit, onDeleteHandler } = useTasksOperations(
        editUrl,
        props
    );

    return (
        <RichPanel>
            <RichPanelHeader>
                <OngoingTaskName task={data} canEdit={canEdit} editUrl={editUrl} />
                <OngoingTaskStatus task={data} canEdit={canEdit} toggleState={toggleStateHandler} />
                <OngoingTaskActions
                    task={data}
                    canEdit={canEdit}
                    onEdit={onEdit}
                    onDelete={onDeleteHandler}
                    toggleDetails={toggleDetails}
                />
            </RichPanelHeader>
            {detailsVisible && <Details {...props} canEdit={canEdit} />}
        </RichPanel>
    );
}